using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Content;

public record StoreImageDto(int Id, string Type, int Width, int Height, string? Title, string? Intro, string? Link,
    string? LinkText, string ImageAddress);
public record StorePageDto(string Name, IReadOnlyDictionary<string, string> Texts, IReadOnlyList<StoreImageDto> Images);
public record PostSummaryDto(int Id, string Slug, string Title, string Type, string? Excerpt, string? BannerImage, DateTimeOffset CreatedAt);
public record PostDto(int Id, string Slug, string Title, string Type, string? Excerpt, string Body, string? BannerImage,
    string? Author, DateTimeOffset CreatedAt);

public static class ContentEndpoints
{
    public static void MapContent(this IEndpointRouteBuilder app)
    {
        // Editable storefront copy and imagery, addressed by page name ("global", "home", ...).
        app.MapGet("/api/content/pages/{name}", async (string name, PlushiesDbContext db) =>
        {
            var page = await db.StorePages.AsNoTracking()
                .Include(p => p.Texts).Include(p => p.Images).ThenInclude(i => i.StoreImageType)
                .FirstOrDefaultAsync(p => p.StorePageName == name) ?? throw ApiException.NotFound("Page");
            return new StorePageDto(page.StorePageName,
                page.Texts.ToDictionary(t => t.TextCode, t => t.TextValue),
                page.Images.OrderBy(i => i.DisplayOrder).Select(i => new StoreImageDto(i.StoreImageInfoId, i.StoreImageType.Name,
                    i.StoreImageType.Width, i.StoreImageType.Height, i.Title, i.Intro, i.Link, i.LinkText, i.ImageAddress)).ToList());
        });

        app.MapGet("/api/posts", async (PlushiesDbContext db, int take = 12) =>
            await db.Posts.AsNoTracking().Where(p => p.Published).OrderByDescending(p => p.CreatedAt).Take(Math.Clamp(take, 1, 50))
                .Select(p => new PostSummaryDto(p.PostId, p.Slug, p.Title, p.PostType.PostTypeTitle, p.Excerpt, p.BannerImage, p.CreatedAt))
                .ToListAsync());

        app.MapGet("/api/posts/{slug}", async (string slug, PlushiesDbContext db) =>
            await db.Posts.AsNoTracking().Where(p => p.Published && p.Slug == slug)
                .Select(p => new PostDto(p.PostId, p.Slug, p.Title, p.PostType.PostTypeTitle, p.Excerpt, p.Body, p.BannerImage,
                    p.User.DisplayName, p.CreatedAt))
                .FirstOrDefaultAsync() ?? throw ApiException.NotFound("Post"));
    }
}
