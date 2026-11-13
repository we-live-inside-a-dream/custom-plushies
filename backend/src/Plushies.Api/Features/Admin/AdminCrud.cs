using Microsoft.EntityFrameworkCore;
using Plushies.Api.Data;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Features.Admin;

public record AdminContext(PlushiesDbContext Db, HttpContext Http, DateTimeOffset Now)
{
    public int UserId => Http.User.SubjectId();
}

public static class AdminCrud
{
    /// <summary>
    /// Maps list / create / update / delete for a simple admin-managed table.
    /// <typeparamref name="TDto"/> is both the response shape and the (validated) request body.
    /// </summary>
    public static void MapCrud<TEntity, TDto>(this RouteGroupBuilder group, string route, string keyProperty,
        Func<IQueryable<TEntity>, IQueryable<TEntity>> shape,
        Func<TEntity, TDto> toDto,
        Func<TEntity> create,
        Func<TEntity, TDto, AdminContext, Task> apply)
        where TEntity : class where TDto : class
    {
        var g = group.MapGroup(route).AddEndpointFilter<ValidationFilter>();

        Task<TEntity?> Find(PlushiesDbContext db, int id) =>
            shape(db.Set<TEntity>()).FirstOrDefaultAsync(e => EF.Property<int>(e, keyProperty) == id);

        g.MapGet("", async (PlushiesDbContext db) =>
            (await shape(db.Set<TEntity>().AsNoTracking()).OrderBy(e => EF.Property<int>(e, keyProperty)).ToListAsync()).Select(toDto));

        g.MapPost("", async (TDto dto, PlushiesDbContext db, HttpContext http, TimeProvider clock) =>
        {
            var entity = create();
            await apply(entity, dto, new AdminContext(db, http, clock.GetUtcNow()));
            db.Add(entity);
            await SaveAsync(db);
            return toDto(entity);
        });

        g.MapPut("{id:int}", async (int id, TDto dto, PlushiesDbContext db, HttpContext http, TimeProvider clock) =>
        {
            var entity = await Find(db, id) ?? throw ApiException.NotFound("Record");
            await apply(entity, dto, new AdminContext(db, http, clock.GetUtcNow()));
            await SaveAsync(db);
            return toDto(entity);
        });

        g.MapDelete("{id:int}", async (int id, PlushiesDbContext db) =>
        {
            var entity = await Find(db, id) ?? throw ApiException.NotFound("Record");
            db.Remove(entity);
            await SaveAsync(db, deleting: true);
            return Results.NoContent();
        });
    }

    public static async Task SaveAsync(PlushiesDbContext db, bool deleting = false)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg)
        {
            throw pg.SqlState switch
            {
                Npgsql.PostgresErrorCodes.UniqueViolation => ApiException.Conflict("That value is already in use — it has to be unique."),
                Npgsql.PostgresErrorCodes.ForeignKeyViolation => ApiException.Conflict(deleting
                    ? "This can't be deleted because other records still use it."
                    : "That refers to a record that doesn't exist."),
                Npgsql.PostgresErrorCodes.CheckViolation => new ApiException("One of the values is out of range."),
                _ => ex,
            };
        }
    }
}
