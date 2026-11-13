using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Catalog;

public record VariantDto(int Id, string Value, string Sku, string TypeTitle, string TypeKind, string? ColorHex,
    PlushieDesign? Design, int PriceCents, int SalePriceCents, int Stock, IReadOnlyList<ImageDto> Images);
public record ImageDto(int Id, string Url, string? ThumbnailUrl);
public record ProductSummaryDto(int Id, string Slug, string Name, string? Brand, string Currency, IReadOnlyList<string> Tags,
    string? Category, string? CategorySlug, int FromPriceCents, int FromSalePriceCents, int PromoPercent,
    double Rating, int ReviewCount, bool InStock, IReadOnlyList<VariantDto> Variants);
public record ProductDetailDto(ProductSummaryDto Summary, string? Description, string? Information,
    IReadOnlyList<ReviewDto> Reviews, IReadOnlyList<ProductSummaryDto> Related);
public record ReviewDto(int Id, string Author, int Rating, string? Comment, DateTimeOffset CreatedAt);
public record CategoryDto(int Id, string Title, string Slug, string? Description, int ProductCount, IReadOnlyList<CategoryDto> Children);
public record PromotionDto(int Id, string Title, string? Description, int DiscountPercent, DateTimeOffset EndDate,
    IReadOnlyList<ProductSummaryDto> Products);

public class ReviewRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")] public int Rating { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }
}

public static class Pricing
{
    /// <summary>Best running promotion for a variant, as a percentage (0 when none).</summary>
    public static int PromoPercent(ProductVariant v, DateTimeOffset now) =>
        v.Promotions.Where(p => p.Promotion.IsRunning(now)).Select(p => p.Promotion.DiscountPercent).DefaultIfEmpty(0).Max();

    public static int SalePrice(ProductVariant v, DateTimeOffset now) => ApplyPercent(v.PriceCents, PromoPercent(v, now));

    public static int ApplyPercent(int cents, int percent) =>
        cents - (int)Math.Round(cents * percent / 100m, MidpointRounding.AwayFromZero);
}

public static class CatalogMapping
{
    public static IQueryable<Product> WithCatalogIncludes(this IQueryable<Product> q) => q
        .Include(p => p.Brand)
        .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
        .Include(p => p.Variants).ThenInclude(v => v.ProductVariantType)
        .Include(p => p.Variants).ThenInclude(v => v.Images)
        .Include(p => p.Variants).ThenInclude(v => v.Promotions).ThenInclude(vp => vp.Promotion)
        .AsSplitQuery();

    public static VariantDto ToDto(this ProductVariant v, DateTimeOffset now) => new(
        v.ProductVariantId, v.ProductVariantValue, v.Sku, v.ProductVariantType.Title, v.ProductVariantType.Type,
        v.ColorHex, v.Design, v.PriceCents, Pricing.SalePrice(v, now), v.Stock,
        v.Images.OrderBy(i => i.DisplayOrder).Select(i => new ImageDto(i.ProductImageId, i.ImageUrl, i.ImageThumbnailUrl)).ToList());

    public static ProductSummaryDto ToSummary(this Product p, DateTimeOffset now, (double Avg, int Count) rating, bool includeInactive = false)
    {
        var variants = p.Variants.Where(v => includeInactive || v.Active).OrderBy(v => v.ProductVariantId).ToList();
        var cheapest = variants.OrderBy(v => Pricing.SalePrice(v, now)).FirstOrDefault();
        var primary = p.ProductCategories.OrderByDescending(pc => pc.IsPrimary).FirstOrDefault()?.Category;
        return new ProductSummaryDto(p.ProductId, p.Slug, p.Name, p.Brand?.Name, p.Currency, p.Tags,
            primary?.Title, primary?.Slug,
            cheapest?.PriceCents ?? 0, cheapest is null ? 0 : Pricing.SalePrice(cheapest, now),
            variants.Select(v => Pricing.PromoPercent(v, now)).DefaultIfEmpty(0).Max(),
            Math.Round(rating.Avg, 1), rating.Count, variants.Any(v => v.Stock > 0),
            variants.Select(v => v.ToDto(now)).ToList());
    }

    public static async Task<Dictionary<int, (double Avg, int Count)>> RatingsAsync(this PlushiesDbContext db, IEnumerable<int> productIds)
    {
        var ids = productIds.ToList();
        var rows = await db.Reviews.Where(r => ids.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(g => new { g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Key, r => (r.Avg, r.Count));
    }

    public static async Task<List<ProductSummaryDto>> ToSummariesAsync(this PlushiesDbContext db, List<Product> products, DateTimeOffset now)
    {
        var ratings = await db.RatingsAsync(products.Select(p => p.ProductId));
        return products.Select(p => p.ToSummary(now, ratings.GetValueOrDefault(p.ProductId))).ToList();
    }
}

public static class CatalogEndpoints
{
    public static void MapCatalog(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async (PlushiesDbContext db) =>
        {
            var all = await db.Categories.AsNoTracking()
                .Select(c => new { c, Count = c.ProductCategories.Count(pc => pc.Product.Active) }).ToListAsync();
            CategoryDto Build(Category c, int count) => new(c.CategoryId, c.Title, c.Slug, c.Description, count,
                all.Where(x => x.c.ParentCategoryId == c.CategoryId).OrderBy(x => x.c.Title).Select(x => Build(x.c, x.Count)).ToList());
            return all.Where(x => x.c.ParentCategoryId is null).OrderBy(x => x.c.Title).Select(x => Build(x.c, x.Count));
        });

        app.MapGet("/api/products", async (PlushiesDbContext db, TimeProvider clock,
            string? category, string? q, string? tag, string? sort, int page = 1, int pageSize = 24) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 60);
            page = Math.Max(page, 1);
            var now = clock.GetUtcNow();
            var query = db.Products.AsNoTracking().Where(p => p.Active && p.Variants.Any(v => v.Active));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.ProductCategories.Any(pc =>
                    pc.Category.Slug == category || pc.Category.ParentCategory!.Slug == category));
            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(p => p.Tags.Contains(tag));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(p => EF.Functions.ILike(p.Name, like) || EF.Functions.ILike(p.Description!, like)
                    || p.Variants.Any(v => EF.Functions.ILike(v.ProductVariantValue, like)));
            }

            query = sort switch
            {
                "price-asc" => query.OrderBy(p => p.Variants.Where(v => v.Active).Min(v => v.PriceCents)),
                "price-desc" => query.OrderByDescending(p => p.Variants.Where(v => v.Active).Min(v => v.PriceCents)),
                "rating" => query.OrderByDescending(p => p.Reviews.Average(r => (double?)r.Rating) ?? 0),
                "name" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.ProductId),
            };

            var total = await query.CountAsync();
            var products = await query.Skip((page - 1) * pageSize).Take(pageSize).WithCatalogIncludes().ToListAsync();
            return new Paged<ProductSummaryDto>(await db.ToSummariesAsync(products, now), total, page, pageSize);
        });

        app.MapGet("/api/products/{slug}", async (string slug, PlushiesDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            var p = await db.Products.AsNoTracking().WithCatalogIncludes().FirstOrDefaultAsync(x => x.Slug == slug && x.Active)
                ?? throw ApiException.NotFound("Product");
            var reviews = await db.Reviews.AsNoTracking().Where(r => r.ProductId == p.ProductId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto(r.ReviewId, r.Customer.FirstName + " " + r.Customer.LastName.Substring(0, 1) + ".",
                    r.Rating, r.Comment, r.CreatedAt)).ToListAsync();

            var categoryIds = p.ProductCategories.Select(pc => pc.CategoryId).ToList();
            var related = await db.Products.AsNoTracking()
                .Where(x => x.Active && x.ProductId != p.ProductId && x.Variants.Any(v => v.Active))
                .OrderByDescending(x => x.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)))
                .ThenBy(x => x.ProductId).Take(4).WithCatalogIncludes().ToListAsync();

            var rating = reviews.Count == 0 ? (0d, 0) : (reviews.Average(r => r.Rating), reviews.Count);
            return new ProductDetailDto(p.ToSummary(now, rating), p.Description, p.Information, reviews,
                await db.ToSummariesAsync(related, now));
        });

        app.MapPost("/api/products/{slug}/reviews", async (string slug, ReviewRequest req, ClaimsPrincipal user,
            PlushiesDbContext db, TimeProvider clock) =>
        {
            var customerId = user.SubjectId();
            var product = await db.Products.FirstOrDefaultAsync(x => x.Slug == slug && x.Active) ?? throw ApiException.NotFound("Product");
            var review = await db.Reviews.FirstOrDefaultAsync(r => r.ProductId == product.ProductId && r.CustomerId == customerId);
            if (review is null)
            {
                review = new Review { ProductId = product.ProductId, CustomerId = customerId, CreatedAt = clock.GetUtcNow() };
                db.Reviews.Add(review);
            }
            review.Rating = req.Rating;
            review.Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policies.Customer).AddEndpointFilter<ValidationFilter>();

        app.MapGet("/api/promotions/home", async (PlushiesDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            var promos = await db.Promotions.AsNoTracking()
                .Where(p => p.DisplayHomePage && p.StartDate <= now && p.EndDate > now)
                .OrderBy(p => p.EndDate).ToListAsync();
            var result = new List<PromotionDto>();
            foreach (var promo in promos)
            {
                var products = await db.Products.AsNoTracking()
                    .Where(p => p.Active && p.Variants.Any(v => v.Active && v.Promotions.Any(vp => vp.PromotionId == promo.PromotionId)))
                    .OrderBy(p => p.ProductId).Take(promo.MaxDisplayNumber).WithCatalogIncludes().ToListAsync();
                result.Add(new PromotionDto(promo.PromotionId, promo.Title, promo.Description, promo.DiscountPercent,
                    promo.EndDate, await db.ToSummariesAsync(products, now)));
            }
            return result;
        });

        var wishlist = app.MapGroup("/api/wishlist").RequireAuthorization(Policies.Customer);
        wishlist.MapGet("", async (ClaimsPrincipal user, PlushiesDbContext db, TimeProvider clock) =>
        {
            var id = user.SubjectId();
            var ids = await db.WishLists.AsNoTracking().Where(w => w.CustomerId == id && w.Product.Active)
                .OrderByDescending(w => w.CreatedAt).Select(w => w.ProductId).ToListAsync();
            var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId)).WithCatalogIncludes().ToListAsync();
            return await db.ToSummariesAsync(products.OrderBy(p => ids.IndexOf(p.ProductId)).ToList(), clock.GetUtcNow());
        });
        wishlist.MapPut("{productId:int}", async (int productId, ClaimsPrincipal user, PlushiesDbContext db, TimeProvider clock) =>
        {
            var id = user.SubjectId();
            if (!await db.Products.AnyAsync(p => p.ProductId == productId)) throw ApiException.NotFound("Product");
            if (!await db.WishLists.AnyAsync(w => w.CustomerId == id && w.ProductId == productId))
            {
                db.WishLists.Add(new WishList { CustomerId = id, ProductId = productId, CreatedAt = clock.GetUtcNow() });
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });
        wishlist.MapDelete("{productId:int}", async (int productId, ClaimsPrincipal user, PlushiesDbContext db) =>
        {
            var id = user.SubjectId();
            await db.WishLists.Where(w => w.CustomerId == id && w.ProductId == productId).ExecuteDeleteAsync();
            return Results.NoContent();
        });
    }
}
