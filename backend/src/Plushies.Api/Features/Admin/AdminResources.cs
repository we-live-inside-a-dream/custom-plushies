using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Domain;
using Plushies.Api.Features.Builder;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Admin;

public static class AdminResources
{
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---- Products (with variants and images) --------------------------------------------------
    private static IQueryable<Product> ProductGraph(PlushiesDbContext db) => db.Products
        .Include(p => p.ProductCategories).Include(p => p.Variants).ThenInclude(v => v.Images).AsSplitQuery();

    private static AdminProductDto ToDto(Product p) => new()
    {
        Id = p.ProductId, Name = p.Name, Slug = p.Slug, BrandId = p.BrandId, Currency = p.Currency, Active = p.Active,
        Description = p.Description, Information = p.Information, Tags = p.Tags, HsTariffCode = p.HsTariffCode,
        OriginCountry = p.OriginCountry, CategoryIds = p.ProductCategories.Select(c => c.CategoryId).ToList(),
        PrimaryCategoryId = p.ProductCategories.FirstOrDefault(c => c.IsPrimary)?.CategoryId,
        Variants = p.Variants.OrderBy(v => v.ProductVariantId).Select(v => new AdminVariantDto
        {
            Id = v.ProductVariantId, ProductVariantTypeId = v.ProductVariantTypeId, Value = v.ProductVariantValue, Sku = v.Sku,
            PriceCents = v.PriceCents, Stock = v.Stock, WeightGrams = v.WeightGrams, ColorHex = v.ColorHex, Design = v.Design,
            Active = v.Active,
            Images = v.Images.OrderBy(i => i.DisplayOrder).Select(i => new AdminImageDto { Id = i.ProductImageId, Url = i.ImageUrl }).ToList(),
        }).ToList(),
    };

    private static async Task ApplyAsync(Product p, AdminProductDto dto, PlushiesDbContext db, DateTimeOffset now)
    {
        if (dto.Variants.GroupBy(v => v.Sku.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ApiException("Two variants share the same SKU. Each SKU must be unique.");
        foreach (var v in dto.Variants)
            if (v.Design is not null && BuilderCatalog.Validate(v.Design) is { } problem)
                throw new ApiException($"Variant \"{v.Value}\": {problem}");

        p.Name = dto.Name.Trim();
        p.Slug = Slug.From(Clean(dto.Slug) ?? dto.Name);
        if (p.Slug.Length == 0) throw new ApiException("The product needs a name with at least one letter or number.");
        p.BrandId = dto.BrandId;
        p.Currency = dto.Currency;
        p.Active = dto.Active;
        p.Description = Clean(dto.Description);
        p.Information = Clean(dto.Information);
        p.Tags = dto.Tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        p.HsTariffCode = Clean(dto.HsTariffCode);
        p.OriginCountry = Clean(dto.OriginCountry)?.ToUpperInvariant();
        p.UpdatedAt = now;

        var categoryIds = dto.CategoryIds.Distinct().ToList();
        var primary = dto.PrimaryCategoryId is { } pc && categoryIds.Contains(pc) ? pc : categoryIds.FirstOrDefault();
        p.ProductCategories.RemoveAll(c => !categoryIds.Contains(c.CategoryId));
        foreach (var cid in categoryIds)
        {
            var link = p.ProductCategories.FirstOrDefault(c => c.CategoryId == cid);
            if (link is null) p.ProductCategories.Add(link = new ProductCategory { CategoryId = cid });
            link.IsPrimary = cid == primary;
        }

        var keep = dto.Variants.Where(v => v.Id > 0).Select(v => v.Id).ToHashSet();
        foreach (var gone in p.Variants.Where(v => v.ProductVariantId > 0 && !keep.Contains(v.ProductVariantId)).ToList())
        {
            // A variant that has been ordered stays (inactive) so order history keeps its link.
            if (await db.OrderItems.AnyAsync(i => i.ProductVariantId == gone.ProductVariantId)) gone.Active = false;
            else p.Variants.Remove(gone);
        }
        foreach (var v in dto.Variants)
        {
            var variant = v.Id > 0 ? p.Variants.FirstOrDefault(x => x.ProductVariantId == v.Id) : null;
            if (variant is null)
            {
                if (v.Id > 0) throw new ApiException($"Variant {v.Id} doesn't belong to this product.");
                variant = new ProductVariant { ProductVariantValue = "", Sku = "", CreatedAt = now };
                p.Variants.Add(variant);
            }
            variant.ProductVariantTypeId = v.ProductVariantTypeId;
            variant.ProductVariantValue = v.Value.Trim();
            variant.Sku = v.Sku.Trim().ToUpperInvariant();
            variant.PriceCents = v.PriceCents;
            variant.Stock = v.Stock;
            variant.WeightGrams = v.WeightGrams;
            variant.ColorHex = Clean(v.ColorHex);
            variant.Design = v.Design;
            variant.Active = v.Active;
            variant.UpdatedAt = now;

            var urls = v.Images.Select(i => i.Url).ToList();
            variant.Images.RemoveAll(i => !urls.Contains(i.ImageUrl));
            for (var order = 0; order < urls.Count; order++)
            {
                var image = variant.Images.FirstOrDefault(i => i.ImageUrl == urls[order]);
                if (image is null) variant.Images.Add(image = new ProductVariantImage { ImageUrl = urls[order], ImageThumbnailUrl = urls[order], CreatedAt = now });
                image.DisplayOrder = order;
            }
        }
    }

    public static void MapProductAdmin(this RouteGroupBuilder admin)
    {
        var products = admin.MapGroup("products").AddEndpointFilter<ValidationFilter>();

        products.MapGet("", async (PlushiesDbContext db, string? q) =>
        {
            var query = db.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                query = query.Where(p => EF.Functions.ILike(p.Name, like) || p.Variants.Any(v => EF.Functions.ILike(v.Sku, like)));
            }
            return await query.OrderBy(p => p.Name).Select(p => new
            {
                id = p.ProductId, p.Name, p.Slug, p.Active, brand = p.Brand!.Name,
                variantCount = p.Variants.Count, stock = p.Variants.Sum(v => (int?)v.Stock) ?? 0,
                fromPriceCents = p.Variants.Min(v => (int?)v.PriceCents) ?? 0,
                design = p.Variants.OrderBy(v => v.ProductVariantId).Select(v => v.Design).FirstOrDefault(),
                imageUrl = p.Variants.SelectMany(v => v.Images).OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            }).ToListAsync();
        });

        products.MapGet("{id:int}", async (int id, PlushiesDbContext db) =>
            ToDto(await ProductGraph(db).AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id) ?? throw ApiException.NotFound("Product")));

        products.MapPost("", async (AdminProductDto dto, PlushiesDbContext db, TimeProvider clock) =>
        {
            var p = new Product { Name = "", Slug = "", CreatedAt = clock.GetUtcNow() };
            await ApplyAsync(p, dto, db, clock.GetUtcNow());
            db.Products.Add(p);
            await AdminCrud.SaveAsync(db);
            return ToDto(p);
        });

        products.MapPut("{id:int}", async (int id, AdminProductDto dto, PlushiesDbContext db, TimeProvider clock) =>
        {
            var p = await ProductGraph(db).FirstOrDefaultAsync(x => x.ProductId == id) ?? throw ApiException.NotFound("Product");
            await ApplyAsync(p, dto, db, clock.GetUtcNow());
            await AdminCrud.SaveAsync(db);
            return ToDto(p);
        });

        products.MapDelete("{id:int}", async (int id, PlushiesDbContext db) =>
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.ProductId == id) ?? throw ApiException.NotFound("Product");
            // Ordered products are retired rather than erased, so past orders still point at them.
            if (await db.OrderItems.AnyAsync(i => i.ProductId == id))
            {
                p.Active = false;
                await db.SaveChangesAsync();
                return Results.Ok(new { retired = true });
            }
            db.Products.Remove(p);
            await AdminCrud.SaveAsync(db, deleting: true);
            return Results.Ok(new { retired = false });
        });

        // Lightweight variant picker for the promotions screen.
        admin.MapGet("variants", async (PlushiesDbContext db) =>
            await db.ProductVariants.AsNoTracking().Where(v => v.Active).OrderBy(v => v.Product.Name).ThenBy(v => v.ProductVariantId)
                .Select(v => new { id = v.ProductVariantId, label = v.Product.Name + " — " + v.ProductVariantValue, v.Sku, v.PriceCents }).ToListAsync());

        admin.MapPost("uploads", async (IFormFile file, IWebHostEnvironment env) =>
        {
            const long maxBytes = 5 * 1024 * 1024;
            if (file.Length is 0 or > maxBytes) throw new ApiException("Images must be between 1 byte and 5 MB.");
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var read = await stream.ReadAsync(header);
            // Trust the bytes, not the file name or the browser's content type.
            var ext = SniffImage(header.AsSpan(0, read)) ?? throw new ApiException("Upload a PNG, JPEG, WebP or GIF image.");
            var dir = Path.Combine(env.ContentRootPath, "uploads");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            stream.Position = 0;
            await using (var target = File.Create(Path.Combine(dir, name))) await stream.CopyToAsync(target);
            return new { url = $"/uploads/{name}" };
        }).DisableAntiforgery(); // Bearer-token API: there is no cookie for a forged form post to ride on.
    }

    private static string? SniffImage(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 8 && h[..8].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ".png";
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h.Length >= 6 && h[..4].SequenceEqual("GIF8"u8)) return ".gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    // ---- Simple tables --------------------------------------------------------------------------
    public static void MapSimpleResources(this RouteGroupBuilder admin)
    {
        admin.MapCrud<Brand, BrandDto>("brands", nameof(Brand.BrandId), q => q,
            e => new BrandDto { Id = e.BrandId, Name = e.Name, Description = e.Description },
            () => new Brand { Name = "" },
            (e, d, _) => { e.Name = d.Name.Trim(); e.Description = Clean(d.Description); return Task.CompletedTask; });

        admin.MapCrud<Category, CategoryAdminDto>("categories", nameof(Category.CategoryId), q => q.Include(c => c.CategoryVariants),
            e => new CategoryAdminDto
            {
                Id = e.CategoryId, Title = e.Title, Slug = e.Slug, ParentCategoryId = e.ParentCategoryId, Description = e.Description,
                VariantTypeIds = e.CategoryVariants.Select(v => v.ProductVariantTypeId).ToList(),
            },
            () => new Category { Title = "", Slug = "" },
            (e, d, ctx) =>
            {
                if (d.ParentCategoryId == e.CategoryId && e.CategoryId != 0) throw new ApiException("A category can't be its own parent.");
                e.Title = d.Title.Trim();
                e.Slug = Slug.From(Clean(d.Slug) ?? d.Title);
                e.ParentCategoryId = d.ParentCategoryId;
                e.Description = Clean(d.Description);
                if (e.CategoryId == 0) e.CreatedAt = ctx.Now;
                e.UpdatedAt = ctx.Now;
                e.CategoryVariants.RemoveAll(v => !d.VariantTypeIds.Contains(v.ProductVariantTypeId));
                foreach (var id in d.VariantTypeIds.Distinct().Where(id => e.CategoryVariants.All(v => v.ProductVariantTypeId != id)))
                    e.CategoryVariants.Add(new CategoryVariant { ProductVariantTypeId = id });
                return Task.CompletedTask;
            });

        admin.MapCrud<ProductVariantType, VariantTypeDto>("variant-types", nameof(ProductVariantType.ProductVariantTypeId), q => q,
            e => new VariantTypeDto { Id = e.ProductVariantTypeId, Title = e.Title, Type = e.Type },
            () => new ProductVariantType { Title = "" },
            (e, d, _) => { e.Title = d.Title.Trim(); e.Type = d.Type; return Task.CompletedTask; });

        admin.MapCrud<Coupon, CouponDto>("coupons", nameof(Coupon.CouponId), q => q,
            e => new CouponDto
            {
                Id = e.CouponId, Code = e.Code, Description = e.Description, DiscountType = e.DiscountType.ToString().ToLowerInvariant(),
                DiscountValue = e.DiscountValue, MinSubtotalCents = e.MinSubtotalCents, StartsAt = e.StartsAt, EndsAt = e.EndsAt,
                MaxRedemptions = e.MaxRedemptions, TimesRedeemed = e.TimesRedeemed, Active = e.Active,
            },
            () => new Coupon { Code = "" },
            (e, d, ctx) =>
            {
                var type = Enum.Parse<DiscountType>(d.DiscountType, true);
                if (type == DiscountType.Percent && d.DiscountValue > 100) throw new ApiException("A percentage coupon can't be more than 100%.");
                if (d.EndsAt <= d.StartsAt) throw new ApiException("The coupon must end after it starts.");
                e.Code = d.Code.Trim().ToUpperInvariant();
                e.Description = Clean(d.Description);
                e.DiscountType = type;
                e.DiscountValue = d.DiscountValue;
                e.MinSubtotalCents = d.MinSubtotalCents;
                e.StartsAt = d.StartsAt?.ToUniversalTime();
                e.EndsAt = d.EndsAt?.ToUniversalTime();
                e.MaxRedemptions = d.MaxRedemptions;
                e.Active = d.Active;
                if (e.CouponId == 0) e.CreatedAt = ctx.Now;
                return Task.CompletedTask;
            });

        admin.MapCrud<Promotion, PromotionAdminDto>("promotions", nameof(Promotion.PromotionId), q => q.Include(p => p.Variants),
            e => new PromotionAdminDto
            {
                Id = e.PromotionId, Title = e.Title, Description = e.Description, DiscountPercent = e.DiscountPercent,
                StartDate = e.StartDate, EndDate = e.EndDate, MaxDisplayNumber = e.MaxDisplayNumber,
                DisplayHomePage = e.DisplayHomePage, VariantIds = e.Variants.Select(v => v.ProductVariantId).ToList(),
            },
            () => new Promotion { Title = "" },
            (e, d, ctx) =>
            {
                if (d.EndDate <= d.StartDate) throw new ApiException("The promotion must end after it starts.");
                e.Title = d.Title.Trim();
                e.Description = Clean(d.Description);
                e.DiscountPercent = d.DiscountPercent;
                e.StartDate = d.StartDate.ToUniversalTime();
                e.EndDate = d.EndDate.ToUniversalTime();
                e.MaxDisplayNumber = d.MaxDisplayNumber;
                e.DisplayHomePage = d.DisplayHomePage;
                if (e.PromotionId == 0) e.CreatedAt = ctx.Now;
                e.ModifiedAt = ctx.Now;
                e.Variants.RemoveAll(v => !d.VariantIds.Contains(v.ProductVariantId));
                foreach (var id in d.VariantIds.Distinct().Where(id => e.Variants.All(v => v.ProductVariantId != id)))
                    e.Variants.Add(new ProductVariantPromotion { ProductVariantId = id });
                return Task.CompletedTask;
            });

        admin.MapCrud<ShippingMethod, ShippingMethodDto>("shipping-methods", nameof(ShippingMethod.ShippingMethodId), q => q,
            e => new ShippingMethodDto
            {
                Id = e.ShippingMethodId, Name = e.Name, Description = e.Description, CountryCode = e.CountryCode, PriceCents = e.PriceCents,
                FreeOverCents = e.FreeOverCents, EstimatedDays = e.EstimatedDays, ChitChatsPostageType = e.ChitChatsPostageType,
                Active = e.Active, DisplayOrder = e.DisplayOrder,
            },
            () => new ShippingMethod { Name = "", CountryCode = "" },
            (e, d, _) =>
            {
                e.Name = d.Name.Trim();
                e.Description = Clean(d.Description);
                e.CountryCode = d.CountryCode.ToUpperInvariant();
                e.PriceCents = d.PriceCents;
                e.FreeOverCents = d.FreeOverCents;
                e.EstimatedDays = Clean(d.EstimatedDays);
                e.ChitChatsPostageType = Clean(d.ChitChatsPostageType);
                e.Active = d.Active;
                e.DisplayOrder = d.DisplayOrder;
                return Task.CompletedTask;
            });

        admin.MapCrud<TaxRate, TaxRateDto>("tax-rates", nameof(TaxRate.TaxRateId), q => q,
            e => new TaxRateDto { Id = e.TaxRateId, CountryCode = e.CountryCode, ProvinceCode = e.ProvinceCode, Label = e.Label, RatePercent = e.RatePercent },
            () => new TaxRate { CountryCode = "", Label = "" },
            (e, d, _) =>
            {
                e.CountryCode = d.CountryCode.ToUpperInvariant();
                e.ProvinceCode = Clean(d.ProvinceCode)?.ToUpperInvariant();
                e.Label = d.Label.Trim();
                e.RatePercent = d.RatePercent;
                return Task.CompletedTask;
            });

        admin.MapCrud<PostType, PostTypeDto>("post-types", nameof(PostType.PostTypeId), q => q,
            e => new PostTypeDto { Id = e.PostTypeId, Title = e.PostTypeTitle },
            () => new PostType { PostTypeTitle = "" },
            (e, d, ctx) =>
            {
                e.PostTypeTitle = d.Title.Trim();
                if (e.PostTypeId == 0) e.CreatedAt = ctx.Now;
                return Task.CompletedTask;
            });

        admin.MapCrud<Post, PostAdminDto>("posts", nameof(Post.PostId), q => q,
            e => new PostAdminDto
            {
                Id = e.PostId, PostTypeId = e.PostTypeId, Title = e.Title, Slug = e.Slug, Excerpt = e.Excerpt, Body = e.Body,
                BannerImage = e.BannerImage, Published = e.Published, CreatedAt = e.CreatedAt,
            },
            () => new Post { Title = "", Slug = "", Body = "" },
            (e, d, ctx) =>
            {
                e.PostTypeId = d.PostTypeId;
                e.Title = d.Title.Trim();
                e.Slug = Slug.From(Clean(d.Slug) ?? d.Title);
                e.Excerpt = Clean(d.Excerpt);
                e.Body = d.Body;
                e.BannerImage = Clean(d.BannerImage);
                e.Published = d.Published;
                if (e.PostId == 0)
                {
                    e.CreatedAt = ctx.Now;
                    e.UserId = ctx.UserId;
                }
                return Task.CompletedTask;
            });
    }

    // ---- Storefront content -------------------------------------------------------------------
    public static void MapContentAdmin(this RouteGroupBuilder admin)
    {
        admin.MapGet("content/pages", async (PlushiesDbContext db) =>
            await db.StorePages.AsNoTracking().OrderBy(p => p.StorePageId).Select(p => new
            {
                id = p.StorePageId, name = p.StorePageName,
                texts = p.Texts.OrderBy(t => t.StoreTextInfoId).Select(t => new { code = t.TextCode, value = t.TextValue, t.UpdatedAt }).ToList(),
            }).ToListAsync());

        admin.MapPut("content/pages/{pageId:int}/texts", async (int pageId, TextsRequest req, PlushiesDbContext db, HttpContext http, TimeProvider clock) =>
        {
            var page = await db.StorePages.Include(p => p.Texts).FirstOrDefaultAsync(p => p.StorePageId == pageId) ?? throw ApiException.NotFound("Page");
            var now = clock.GetUtcNow();
            foreach (var (code, value) in req.Texts)
            {
                var text = page.Texts.FirstOrDefault(t => t.TextCode == code);
                if (text is null) page.Texts.Add(text = new StoreTextInfo { TextCode = code, TextValue = value, CreatedAt = now });
                if (text.TextValue != value || text.UpdatedAt == default)
                {
                    text.TextValue = value;
                    text.UserId = http.User.SubjectId();
                    text.UpdatedAt = now;
                }
            }
            page.UpdatedAt = now;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter>();

        admin.MapCrud<StoreImageType, StoreImageTypeDto>("store-image-types", nameof(StoreImageType.StoreImageTypeId), q => q,
            e => new StoreImageTypeDto { Id = e.StoreImageTypeId, Name = e.Name, Width = e.Width, Height = e.Height, MaxCount = e.MaxCount },
            () => new StoreImageType { Name = "" },
            (e, d, ctx) =>
            {
                e.Name = d.Name.Trim();
                e.Width = d.Width;
                e.Height = d.Height;
                e.MaxCount = d.MaxCount;
                if (e.StoreImageTypeId == 0) e.CreatedAt = ctx.Now;
                e.UpdatedAt = ctx.Now;
                return Task.CompletedTask;
            });

        admin.MapCrud<StoreImageInfo, StoreImageAdminDto>("store-images", nameof(StoreImageInfo.StoreImageInfoId), q => q,
            e => new StoreImageAdminDto
            {
                Id = e.StoreImageInfoId, StorePageId = e.StorePageId, StoreImageTypeId = e.StoreImageTypeId, Title = e.Title,
                Intro = e.Intro, Link = e.Link, LinkText = e.LinkText, ImageAddress = e.ImageAddress, DisplayOrder = e.DisplayOrder,
            },
            () => new StoreImageInfo { ImageAddress = "" },
            async (e, d, ctx) =>
            {
                // Enforce the slot's max_count from store_image_type.
                var type = await ctx.Db.StoreImageTypes.FirstOrDefaultAsync(t => t.StoreImageTypeId == d.StoreImageTypeId)
                    ?? throw new ApiException("Unknown image type.");
                var used = await ctx.Db.StoreImageInfos.CountAsync(i => i.StorePageId == d.StorePageId
                    && i.StoreImageTypeId == d.StoreImageTypeId && i.StoreImageInfoId != e.StoreImageInfoId);
                if (used >= type.MaxCount)
                    throw ApiException.Conflict($"\"{type.Name}\" allows at most {type.MaxCount} image(s) per page. Remove one first.");
                e.StorePageId = d.StorePageId;
                e.StoreImageTypeId = d.StoreImageTypeId;
                e.Title = Clean(d.Title);
                e.Intro = Clean(d.Intro);
                e.Link = Clean(d.Link);
                e.LinkText = Clean(d.LinkText);
                e.ImageAddress = d.ImageAddress;
                e.DisplayOrder = d.DisplayOrder;
                e.UserId = ctx.UserId;
                if (e.StoreImageInfoId == 0) e.CreatedAt = ctx.Now;
                e.UpdatedAt = ctx.Now;
            });
    }
}
