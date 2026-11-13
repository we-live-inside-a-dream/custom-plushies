using Microsoft.EntityFrameworkCore;
using Plushies.Api.Domain;
using Plushies.Api.Infrastructure;

namespace Plushies.Api.Data;

/// <summary>
/// Fills an empty database. Reference data (pages, shipping, tax, variant types) is always seeded;
/// the admin user comes from Seed:AdminEmail / Seed:AdminPassword; the demo catalogue only when
/// Seed:DemoData is true.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(PlushiesDbContext db, PasswordService passwords, IConfiguration config, ILogger log)
    {
        var now = DateTimeOffset.UtcNow;

        var adminEmail = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var adminPassword = config["Seed:AdminPassword"];
        if (!await db.Users.AnyAsync())
        {
            if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
            {
                db.Users.Add(new User { Email = adminEmail, Password = passwords.Hash(adminPassword), DisplayName = "Shop Admin", Role = UserRole.Admin, CreatedAt = now });
                await db.SaveChangesAsync();
                log.LogInformation("Created admin user {Email}", adminEmail);
            }
            else
            {
                log.LogWarning("No admin user exists. Set Seed:AdminEmail and Seed:AdminPassword to create one on startup.");
            }
        }

        if (!await db.StorePages.AnyAsync()) await SeedReferenceDataAsync(db, now);
        if (config.GetValue<bool>("Seed:DemoData") && !await db.Products.AnyAsync()) await SeedDemoAsync(db, passwords, now);
    }

    private static async Task SeedReferenceDataAsync(PlushiesDbContext db, DateTimeOffset now)
    {
        StorePage Page(string name, params (string Code, string Value)[] texts) => new()
        {
            StorePageName = name, CreatedAt = now, UpdatedAt = now,
            Texts = texts.Select(t => new StoreTextInfo { TextCode = t.Code, TextValue = t.Value, CreatedAt = now, UpdatedAt = now }).ToList(),
        };

        db.StorePages.AddRange(
            Page("global",
                ("store_name", "Stitchling"),
                ("announcement", "Free Canadian shipping over $90 · Every plushie is stitched to order"),
                ("footer_blurb", "A tiny workshop making very soft friends. Designed by you, stitched by us, hugged by whoever is lucky."),
                ("support_email", "hello@stitchling.test")),
            Page("home",
                ("hero_kicker", "Custom plushies, stitched to order"),
                ("hero_title", "You dream it. We stitch it."),
                ("hero_subtitle", "Pick a critter, choose the fabrics, add a name. We sew your one-of-a-kind plushie by hand and send it off for a lifetime of hugs."),
                ("hero_cta_primary", "Build your plushie"),
                ("hero_cta_secondary", "Shop ready-made"),
                ("builder_title", "Make one nobody else has"),
                ("builder_body", "Six critters, sixteen fabrics, four kinds of eyes and a wardrobe of tiny accessories. The preview updates as you play."),
                ("steps_title", "From doodle to doorstep")),
            Page("about",
                ("title", "Hello, we make soft things"),
                ("body", "Stitchling started with one lopsided bear and a sewing machine older than its owner. Today every plushie is still cut, stitched and stuffed by hand.")));

        db.StoreImageTypes.AddRange(
            new StoreImageType { Name = "hero_banner", Width = 1600, Height = 700, MaxCount = 3, CreatedAt = now, UpdatedAt = now },
            new StoreImageType { Name = "promo_tile", Width = 800, Height = 800, MaxCount = 4, CreatedAt = now, UpdatedAt = now });

        db.PostTypes.AddRange(
            new PostType { PostTypeTitle = "Journal", CreatedAt = now },
            new PostType { PostTypeTitle = "Announcement", CreatedAt = now });

        db.ProductVariantTypes.AddRange(
            new ProductVariantType { Title = "Colour", Type = "color" },
            new ProductVariantType { Title = "Size", Type = "size" });

        // Flat storefront rates. The real postage is bought per shipment through Chit Chats.
        db.ShippingMethods.AddRange(
            new ShippingMethod { Name = "Canada Tracked", Description = "Tracked delivery anywhere in Canada", CountryCode = "CA", PriceCents = 950, FreeOverCents = 9000, EstimatedDays = "2–8 business days", ChitChatsPostageType = "chit_chats_canada_tracked", DisplayOrder = 1 },
            new ShippingMethod { Name = "U.S. Tracked", Description = "Tracked delivery to the United States", CountryCode = "US", PriceCents = 1250, FreeOverCents = 12000, EstimatedDays = "3–10 business days", ChitChatsPostageType = "chit_chats_us_edge", DisplayOrder = 2 },
            new ShippingMethod { Name = "International Tracked", Description = "Tracked delivery worldwide", CountryCode = "*", PriceCents = 2200, EstimatedDays = "6–15 business days", ChitChatsPostageType = "chit_chats_international_tracked", DisplayOrder = 3 });

        // Starting point only: confirm current rates with your accountant before taking real orders.
        (string Province, string Label, decimal Rate)[] canada =
        [
            ("AB", "GST", 5m), ("BC", "GST + PST", 12m), ("MB", "GST + RST", 12m), ("NB", "HST", 15m), ("NL", "HST", 15m),
            ("NS", "HST", 14m), ("NT", "GST", 5m), ("NU", "GST", 5m), ("ON", "HST", 13m), ("PE", "HST", 15m),
            ("QC", "GST + QST", 14.975m), ("SK", "GST + PST", 11m), ("YT", "GST", 5m),
        ];
        db.TaxRates.AddRange(canada.Select(t => new TaxRate { CountryCode = "CA", ProvinceCode = t.Province, Label = t.Label, RatePercent = t.Rate }));

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoAsync(PlushiesDbContext db, PasswordService passwords, DateTimeOffset now)
    {
        var colour = await db.ProductVariantTypes.FirstAsync(t => t.Type == "color");
        var size = await db.ProductVariantTypes.FirstAsync(t => t.Type == "size");
        var brand = new Brand { Name = "Stitchling Originals", Description = "Designed and sewn in our own workshop." };
        var guest = new Brand { Name = "Guest Makers", Description = "Limited runs from makers we love." };

        Category Cat(string title, string description, Category? parent = null) => new()
        {
            Title = title, Slug = Slug.From(title), Description = description, ParentCategory = parent, CreatedAt = now, UpdatedAt = now,
            CategoryVariants = [new CategoryVariant { ProductVariantType = colour }],
        };
        var ready = Cat("Ready to hug", "In stock and ready to ship.");
        var bears = Cat("Bears", "Round ears, rounder bellies.", ready);
        var bunnies = Cat("Bunnies", "Long ears for extra listening.", ready);
        var cats = Cat("Cats", "Will sit on your keyboard.", ready);
        var critters = Cat("Critters", "Frogs, dinos and other small weirdos.", ready);
        var jumbo = Cat("Jumbo", "Bigger than your pillow.");
        jumbo.CategoryVariants.Add(new CategoryVariant { ProductVariantType = size });

        const string care = "Outer: 100% polyester minky. Filling: recycled polyester fibre. Embroidered details, no loose parts. Spot clean, or machine wash cold in a laundry bag and air dry. Suitable from birth.";
        var skuCounter = 0;

        ProductVariant V(string value, string prefix, int price, int stock, PlushieDesign design, int weight = 320, ProductVariantType? type = null) => new()
        {
            ProductVariantType = type ?? colour, ProductVariantValue = value, Sku = $"{prefix}-{++skuCounter:000}", PriceCents = price, Stock = stock,
            WeightGrams = weight, ColorHex = design.Body, Design = design, CreatedAt = now, UpdatedAt = now,
        };
        Product P(string name, string description, Category category, string[] tags, int daysAgo, Brand b, params ProductVariant[] variants)
        {
            var p = new Product
            {
                Name = name, Slug = Slug.From(name), Brand = b, Description = description, Information = care, Tags = [.. tags],
                OriginCountry = "CA", CreatedAt = now.AddDays(-daysAgo), UpdatedAt = now, Variants = [.. variants],
            };
            p.ProductCategories.Add(new ProductCategory { Category = category, IsPrimary = true });
            return p;
        }
        PlushieDesign D(string species, string body, string belly = "#FFF1D6", string eyes = "button", string mouth = "smile",
            bool cheeks = true, string accessory = "none", string accessoryColor = "#FF5DA2") =>
            new() { Species = species, Body = body, Belly = belly, Eyes = eyes, Mouth = mouth, Cheeks = cheeks, Accessory = accessory, AccessoryColor = accessoryColor };

        var molly = P("Molly the Bear", "The original Stitchling. Molly has a wobbly sit, a belly made for resting chins on, and absolutely no thoughts behind those button eyes.",
            bears, ["bestseller"], 60, brand,
            V("Blueberry", "MOLLY", 3600, 14, D("bear", "#4A6CF7", accessory: "bow", accessoryColor: "#FFD84D")),
            V("Midnight", "MOLLY", 3600, 9, D("bear", "#2A2350", "#C9B6FF", eyes: "sparkle", accessory: "bow", accessoryColor: "#FF5DA2")),
            V("Honey (limited)", "MOLLY", 4200, 3, D("bear", "#FFD84D", accessory: "bow", accessoryColor: "#F2553D")));
        var biscuit = P("Biscuit the Bunny", "Biscuit's ears are exactly the right length for holding while you fall asleep. Comes pre-loaded with one (1) blep.",
            bunnies, ["bestseller", "new"], 12, brand,
            V("Strawberry", "BISCUIT", 3800, 11, D("bunny", "#FF5DA2", mouth: "tongue")),
            V("Oat", "BISCUIT", 3800, 8, D("bunny", "#EBD3A8", "#FFFFFF", mouth: "tongue", accessory: "flower", accessoryColor: "#FF5DA2")),
            V("Lilac", "BISCUIT", 3800, 6, D("bunny", "#C9B6FF", "#FFFFFF", eyes: "sleepy")));
        var mochi = P("Mochi the Cat", "Mochi does not come when called. Mochi does, however, fit perfectly under one arm and has never once knocked a glass off a table.",
            cats, ["new"], 8, brand,
            V("Cloud", "MOCHI", 3800, 10, D("cat", "#FFFFFF", "#FFE3EE", mouth: "kitty")),
            V("Tangerine", "MOCHI", 3800, 7, D("cat", "#FF8A3D", mouth: "kitty", accessory: "scarf", accessoryColor: "#36E0B0")),
            V("Charcoal", "MOCHI", 3800, 0, D("cat", "#2A2350", "#A9A6B8", eyes: "sparkle", mouth: "kitty")));
        var pesto = P("Pesto the Frog", "A frog of few words and wide smiles. Pesto sits flat on any shelf, desk or forehead.",
            critters, ["bestseller"], 30, brand,
            V("Pesto", "PESTO", 4000, 12, D("frog", "#7BC950", "#E9F7C8")),
            V("Mint", "PESTO", 4000, 5, D("frog", "#36E0B0", "#EAFFF7", accessory: "crown", accessoryColor: "#FFD84D")));
        var rex = P("Rex the Tiny Dino", "65 million years old and still can't reach the top shelf. Rex's spikes are extremely soft and only slightly intimidating.",
            critters, ["new"], 5, brand,
            V("Mint", "REX", 4400, 9, D("dino", "#36E0B0", "#FFF1D6", accessoryColor: "#FF8A3D")),
            V("Grape", "REX", 4400, 6, D("dino", "#8B5CF6", "#FFF1D6", accessoryColor: "#FFD84D")),
            V("Tomato", "REX", 4400, 2, D("dino", "#F2553D", "#FFF1D6", eyes: "sparkle", accessoryColor: "#FFD84D")));
        var noodle = P("Noodle the Axolotl", "Permanently delighted. Noodle's frilly gills are the softest part and yes, you're allowed to boop them.",
            critters, ["new"], 3, guest,
            V("Bubblegum", "NOODLE", 4600, 8, D("axolotl", "#FFB3D1", "#FFF1D6", accessoryColor: "#FF5DA2")),
            V("Sky", "NOODLE", 4600, 4, D("axolotl", "#5CC8FF", "#FFFFFF", eyes: "sparkle", accessoryColor: "#4A6CF7")));
        var hug = P("Hug-Size Molly", "Molly, but the size of a toddler. For when a regular hug simply won't cover it.",
            jumbo, ["gift"], 20, brand,
            V("Classic · 30 cm", "BIGMOLLY", 4800, 6, D("bear", "#FF8A3D", accessory: "scarf", accessoryColor: "#4A6CF7"), 320, size),
            V("Jumbo · 50 cm", "BIGMOLLY", 7800, 3, D("bear", "#FF8A3D", accessory: "scarf", accessoryColor: "#4A6CF7"), 800, size));
        hug.ProductCategories.Add(new ProductCategory { Category = bears });
        var prince = P("Pickle the Frog Prince", "Kiss at your own risk. Pickle arrives wearing a tiny felt crown and an expression of total confidence.",
            critters, ["gift"], 40, guest,
            V("Royal Pesto", "PICKLE", 4800, 5, D("frog", "#7BC950", "#E9F7C8", eyes: "sleepy", accessory: "crown", accessoryColor: "#FFD84D")));

        db.Products.AddRange(molly, biscuit, mochi, pesto, rex, noodle, hug, prince);

        db.Promotions.Add(new Promotion
        {
            Title = "Cosy season sale", Description = "15% off a few favourites while the kettle's on.", DiscountPercent = 15,
            StartDate = now.AddDays(-2), EndDate = now.AddDays(21), MaxDisplayNumber = 4, DisplayHomePage = true, CreatedAt = now, ModifiedAt = now,
            Variants = [.. molly.Variants.Take(2).Concat(pesto.Variants).Concat(mochi.Variants.Take(1))
                .Select(v => new ProductVariantPromotion { ProductVariant = v })],
        });

        db.Coupons.AddRange(
            new Coupon { Code = "WELCOME10", Description = "10% off your first order", DiscountType = DiscountType.Percent, DiscountValue = 10, CreatedAt = now },
            new Coupon { Code = "FREEHUG", Description = "$5 off orders over $50", DiscountType = DiscountType.Fixed, DiscountValue = 500, MinSubtotalCents = 5000, CreatedAt = now });

        Customer C(string first, string last, string email) => new()
        {
            FirstName = first, LastName = last, Email = email, Password = passwords.Hash("plushie-demo-123"), CreatedAt = now.AddDays(-30), UpdatedAt = now,
        };
        var demo = C("Robin", "Hartley", "demo@stitchling.test");
        demo.Addresses.Add(new CustomerAddress
        {
            RecipientName = "Robin Hartley", Address1 = "123 Maple Street", Address2 = "Unit 4", City = "Toronto", ProvinceCode = "ON",
            PostalCode = "M5V 2T6", CountryCode = "CA", Phone = "416-555-0199", IsDefault = true, CreatedAt = now, UpdatedAt = now,
        });
        var sam = C("Sam", "Okafor", "sam@stitchling.test");
        var june = C("June", "Lavoie", "june@stitchling.test");
        db.Customers.AddRange(demo, sam, june);

        Review R(Product p, Customer c, int rating, string comment, int daysAgo) =>
            new() { Product = p, Customer = c, Rating = rating, Comment = comment, CreatedAt = now.AddDays(-daysAgo) };
        db.Reviews.AddRange(
            R(molly, demo, 5, "Softer than I thought possible. My niece has not put Molly down in two weeks.", 14),
            R(molly, sam, 5, "The stitching is so neat. Bought the Midnight one for my desk.", 9),
            R(molly, june, 4, "Gorgeous bear, slightly smaller than I pictured but very huggable.", 3),
            R(biscuit, sam, 5, "The ears! Perfect gift, arrived beautifully wrapped.", 6),
            R(pesto, june, 5, "Pesto lives on my monitor now. 10/10 frog.", 11),
            R(pesto, demo, 4, "Very cute, very round.", 5),
            R(rex, june, 5, "Tiny arms, huge personality.", 2),
            R(noodle, sam, 5, "Best axolotl I own (I own several).", 1));

        await db.SaveChangesAsync();

        if (await db.Users.FirstOrDefaultAsync() is { } author)
        {
            var journal = await db.PostTypes.FirstAsync(t => t.PostTypeTitle == "Journal");
            var news = await db.PostTypes.FirstAsync(t => t.PostTypeTitle == "Announcement");
            db.Posts.AddRange(
                new Post
                {
                    PostType = journal, UserId = author.UserId, Title = "How a Stitchling gets made", Slug = "how-a-stitchling-gets-made",
                    Excerpt = "Six steps, forty-odd pattern pieces and one very important squish test.",
                    Body = "<p>Every plushie starts as a paper pattern. We trace it onto minky, cut each piece by hand and pin the lot together like a very soft jigsaw.</p><h2>Stitch, turn, stuff</h2><p>Faces are embroidered <em>before</em> the head is sewn up — it's the only way to get the eyes level. Then everything is turned right-side out and filled, a handful at a time, until it passes the squish test.</p><ul><li>Too firm: no good for hugging.</li><li>Too floppy: can't sit up for tea.</li><li>Just right: bounces back in about a second.</li></ul><p>The last stitch is a hidden ladder stitch at the back. If you can find it, we owe you a sticker.</p>",
                    CreatedAt = now.AddDays(-10),
                },
                new Post
                {
                    PostType = news, UserId = author.UserId, Title = "Say hi to Noodle", Slug = "say-hi-to-noodle",
                    Excerpt = "Our first axolotl has frilly gills, a permanent grin and two colourways.",
                    Body = "<p>You asked for something a bit weirder. Noodle the Axolotl is here in Bubblegum and Sky, with gills soft enough to justify the extra hour of sewing each one takes.</p><p>Want a Noodle in your own colours? The <a href=\"/build\">builder</a> has axolotls now too.</p>",
                    CreatedAt = now.AddDays(-3),
                });
            await db.SaveChangesAsync();
        }
    }
}
