using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Plushies.Api.Domain;

namespace Plushies.Api.Data;

public class PlushiesDbContext(DbContextOptions<PlushiesDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductVariantType> ProductVariantTypes => Set<ProductVariantType>();
    public DbSet<CategoryVariant> CategoryVariants => Set<CategoryVariant>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantImage> ProductVariantImages => Set<ProductVariantImage>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<ProductVariantPromotion> ProductVariantPromotions => Set<ProductVariantPromotion>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishList> WishLists => Set<WishList>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CustomDesign> CustomDesigns => Set<CustomDesign>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PostType> PostTypes => Set<PostType>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<StorePage> StorePages => Set<StorePage>();
    public DbSet<StoreTextInfo> StoreTextInfos => Set<StoreTextInfo>();
    public DbSet<StoreImageType> StoreImageTypes => Set<StoreImageType>();
    public DbSet<StoreImageInfo> StoreImageInfos => Set<StoreImageInfo>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums are stored as readable snake_case text ("pending_payment"), not integers.
        builder.Properties<OrderStatus>().HaveConversion<SnakeCaseEnumConverter<OrderStatus>>().HaveMaxLength(32);
        builder.Properties<PaymentStatus>().HaveConversion<SnakeCaseEnumConverter<PaymentStatus>>().HaveMaxLength(32);
        builder.Properties<DiscountType>().HaveConversion<SnakeCaseEnumConverter<DiscountType>>().HaveMaxLength(32);
        builder.Properties<CustomDesignStatus>().HaveConversion<SnakeCaseEnumConverter<CustomDesignStatus>>().HaveMaxLength(32);
        builder.Properties<UserRole>().HaveConversion<SnakeCaseEnumConverter<UserRole>>().HaveMaxLength(32);
        builder.Properties<PlushieDesign>().HaveConversion<PlushieDesignConverter, PlushieDesignComparer>().HaveColumnType("jsonb");
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Table names follow the diagram: singular snake_case. "order" and "user" are reserved
        // words in PostgreSQL, so those two are pluralised.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(entity.ClrType.Name);
            entity.SetTableName(name switch { "order" => "orders", "user" => "users", _ => name });
        }

        b.Entity<Brand>(e => e.HasIndex(x => x.Name).IsUnique());

        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.ParentCategory).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ProductCategory>(e =>
        {
            e.HasKey(x => new { x.CategoryId, x.ProductId });
            e.HasOne(x => x.Category).WithMany(x => x.ProductCategories).HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.Product).WithMany(x => x.ProductCategories).HasForeignKey(x => x.ProductId);
        });

        b.Entity<CategoryVariant>(e =>
        {
            e.HasKey(x => new { x.CategoryId, x.ProductVariantTypeId });
            e.HasOne(x => x.Category).WithMany(x => x.CategoryVariants).HasForeignKey(x => x.CategoryId);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.ProductVariantType).WithMany().HasForeignKey(x => x.ProductVariantTypeId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_product_variant_price", "price_cents >= 0");
                t.HasCheckConstraint("ck_product_variant_stock", "stock >= 0");
            });
        });

        b.Entity<ProductVariantImage>(e =>
        {
            e.HasKey(x => x.ProductImageId);
            e.HasOne(x => x.ProductVariant).WithMany(x => x.Images).HasForeignKey(x => x.ProductVariantId);
        });

        b.Entity<ProductVariantPromotion>(e =>
        {
            e.HasKey(x => new { x.ProductVariantId, x.PromotionId });
            e.HasOne(x => x.ProductVariant).WithMany(x => x.Promotions).HasForeignKey(x => x.ProductVariantId);
            e.HasOne(x => x.Promotion).WithMany(x => x.Variants).HasForeignKey(x => x.PromotionId);
        });

        b.Entity<Promotion>(e => e.ToTable(t =>
            t.HasCheckConstraint("ck_promotion_discount", "discount_percent BETWEEN 0 AND 100")));

        b.Entity<Review>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.CustomerId }).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.Reviews).HasForeignKey(x => x.ProductId);
            e.ToTable(t => t.HasCheckConstraint("ck_review_rating", "rating BETWEEN 1 AND 5"));
        });

        b.Entity<WishList>(e =>
        {
            e.HasKey(x => x.WishlistId);
            e.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
        });

        b.Entity<Customer>(e => e.HasIndex(x => x.Email).IsUnique());
        b.Entity<CustomerAddress>(e =>
        {
            e.HasOne(x => x.Customer).WithMany(x => x.Addresses).HasForeignKey(x => x.CustomerId);
            e.Property(x => x.CountryCode).HasMaxLength(2);
        });

        b.Entity<Cart>(e =>
        {
            e.HasIndex(x => x.SessionToken).IsUnique();
            e.HasIndex(x => x.CustomerId).IsUnique();
            e.HasOne(x => x.Coupon).WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CartItem>(e =>
        {
            e.HasOne(x => x.Cart).WithMany(x => x.Items).HasForeignKey(x => x.CartId);
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CustomDesign).WithMany().HasForeignKey(x => x.CustomDesignId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_cart_item_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_cart_item_target", "(product_variant_id IS NULL) <> (custom_design_id IS NULL)");
            });
        });

        b.Entity<Coupon>(e => e.HasIndex(x => x.Code).IsUnique());

        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ShippingMethod).WithMany().HasForeignKey(x => x.ShippingMethodId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.ShipCountryCode).HasMaxLength(2);
        });

        b.Entity<OrderItem>(e =>
        {
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CustomDesign).WithMany().HasForeignKey(x => x.CustomDesignId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Payment>(e => e.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId));

        b.Entity<Shipment>(e =>
        {
            e.HasIndex(x => x.ChitChatsShipmentId).IsUnique();
            e.HasOne(x => x.Order).WithMany(x => x.Shipments).HasForeignKey(x => x.OrderId);
            e.Property(x => x.RatesJson).HasColumnType("jsonb");
            e.Property(x => x.SizeXCm).HasPrecision(8, 2);
            e.Property(x => x.SizeYCm).HasPrecision(8, 2);
            e.Property(x => x.SizeZCm).HasPrecision(8, 2);
            e.Ignore(x => x.IsTerminal);
        });

        b.Entity<TaxRate>(e =>
        {
            e.HasIndex(x => new { x.CountryCode, x.ProvinceCode }).IsUnique();
            e.Property(x => x.RatePercent).HasPrecision(6, 3);
        });

        b.Entity<User>(e => e.HasIndex(x => x.Email).IsUnique());
        b.Entity<Post>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PostType).WithMany().HasForeignKey(x => x.PostTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StorePage>(e => e.HasIndex(x => x.StorePageName).IsUnique());
        b.Entity<StoreTextInfo>(e =>
        {
            e.HasIndex(x => new { x.StorePageId, x.TextCode }).IsUnique();
            e.HasOne(x => x.StorePage).WithMany(x => x.Texts).HasForeignKey(x => x.StorePageId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<StoreImageInfo>(e =>
        {
            e.HasOne(x => x.StorePage).WithMany(x => x.Images).HasForeignKey(x => x.StorePageId);
            e.HasOne(x => x.StoreImageType).WithMany().HasForeignKey(x => x.StoreImageTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

public class SnakeCaseEnumConverter<T>() : ValueConverter<T, string>(
    v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()),
    v => Enum.Parse<T>(v.Replace("_", ""), true))
    where T : struct, Enum;

public class PlushieDesignConverter() : ValueConverter<PlushieDesign, string>(
    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Web),
    v => JsonSerializer.Deserialize<PlushieDesign>(v, JsonSerializerOptions.Web)!);

public class PlushieDesignComparer() : ValueComparer<PlushieDesign>((a, b) => a == b, v => v.GetHashCode(), v => v);
