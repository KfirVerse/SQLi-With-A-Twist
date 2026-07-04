/* ============================================================================
   MSSQL "SQLi -> RCE" Lab : database seed (init.sql)
   INTENTIONALLY INSECURE. Localhost / isolated network only.
   Password for the webapp login is injected by entrypoint.sh via sqlcmd:
       sqlcmd ... -v WEBAPP_PASSWORD="<from .env>" -i init.sql
   ============================================================================ */

:on error exit
SET NOCOUNT ON;
GO

/* 1. Application database ------------------------------------------------- */
IF DB_ID('StoreDb') IS NULL
BEGIN
    PRINT 'Creating database StoreDb...';
    CREATE DATABASE StoreDb;
END
GO

USE StoreDb;
GO

/* 2. Product catalogue (~55 fictional rows, ROUND integer prices) --------- */
IF OBJECT_ID('dbo.products', 'U') IS NOT NULL
    DROP TABLE dbo.products;
GO

CREATE TABLE dbo.products (
    id          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    name        NVARCHAR(200)  NOT NULL,
    category    NVARCHAR(100)  NOT NULL,
    price       INT            NOT NULL,
    description NVARCHAR(1000) NULL,
    active      BIT            NOT NULL CONSTRAINT DF_products_active DEFAULT (1)
);
GO

INSERT INTO dbo.products (name, category, price, description, active) VALUES
('Aurora Desk Lamp',        'Lighting',   40, 'Adjustable LED desk lamp with warm and cool modes.', 1),
('Cirrus Cloud Diffuser',   'Home',       28, 'Ultrasonic aroma diffuser with a soft night light.', 1),
('Everest Water Bottle',    'Outdoor',    22, 'Vacuum-insulated 750ml stainless steel bottle.', 1),
('Cobalt Ceramic Mug',      'Kitchen',    13, 'Glazed stoneware mug, 350ml, dishwasher safe.', 1),
('Meridian Wall Clock',     'Home',       35, 'Silent sweep quartz wall clock with a walnut rim.', 1),
('Pioneer Trail Backpack',  'Outdoor',    65, '28L weather-resistant hiking backpack.', 1),
('Solstice Reading Light',  'Lighting',   18, 'Clip-on rechargeable book light, three levels.', 1),
('Harbor Chef Knife',       'Kitchen',    49, '8-inch high-carbon stainless steel chef knife.', 1),
('Cascade Shower Speaker',  'Audio',      26, 'Waterproof Bluetooth speaker with suction mount.', 1),
('Verdant Herb Planter',    'Garden',     16, 'Self-watering indoor herb planter, set of three.', 1),
('Atlas Laptop Stand',      'Office',     42, 'Aluminium adjustable laptop riser with cable slot.', 1),
('Lumen Smart Bulb',        'Lighting',   15, 'Wi-Fi RGB smart bulb, 800 lumens, app controlled.', 1),
('Drift Memory Pillow',     'Bedroom',    38, 'Contour memory-foam pillow with a bamboo cover.', 1),
('Quartz Kitchen Scale',    'Kitchen',    21, 'Digital kitchen scale, 5kg capacity, tare function.', 1),
('Vantage Monitor Arm',     'Office',     58, 'Gas-spring single monitor mount, VESA compatible.', 1),
('Ridge Travel Mug',        'Kitchen',    20, 'Leak-proof 450ml travel mug with a flip lid.', 1),
('Beacon Bike Light Set',   'Outdoor',    27, 'USB rechargeable front and rear bicycle lights.', 1),
('Marlow Throw Blanket',    'Home',       33, 'Chunky knit throw blanket, 130x170cm, oatmeal.', 1),
('Pulse Fitness Band',      'Fitness',    45, 'Heart-rate fitness tracker with sleep monitoring.', 1),
('Copper Moscow Mug',       'Kitchen',    18, 'Hammered copper mug, 500ml, hand polished.', 1),
('Summit Camp Stove',       'Outdoor',    52, 'Compact single-burner backpacking stove.', 1),
('Halo Ring Light',         'Lighting',   36, '10-inch LED ring light with a phone clamp.', 1),
('Alcove Bookend Pair',     'Office',     15, 'Solid steel bookends with a matte black finish.', 1),
('Tidal Beach Towel',       'Outdoor',    24, 'Quick-dry microfibre beach towel, sand resistant.', 1),
('Grove Cutting Board',     'Kitchen',    30, 'End-grain acacia cutting board with juice groove.', 1),
('Zephyr Desk Fan',         'Office',     31, 'Quiet USB desk fan with three speeds and tilt.', 1),
('Lyric Wireless Earbuds',  'Audio',      60, 'In-ear wireless earbuds with a charging case.', 1),
('Fable Storybook Lamp',    'Lighting',   27, 'Kids projector night lamp with rotating scenes.', 1),
('Terra Plant Mister',      'Garden',     10, 'Fine-mist glass spray bottle for houseplants.', 1),
('Onyx Phone Dock',         'Office',     23, 'Weighted aluminium desktop phone dock.', 1),
('Cove Picnic Blanket',     'Outdoor',    29, 'Foldable waterproof-backed picnic blanket.', 1),
('Sage Tea Infuser',        'Kitchen',    12, 'Stainless steel loose-leaf tea infuser.', 1),
('Willow Laundry Basket',   'Home',       27, 'Collapsible woven laundry basket with handles.', 1),
('Amber Salt Lamp',         'Lighting',   32, 'Natural Himalayan salt lamp with a dimmer cable.', 1),
('Basin Toothbrush Holder', 'Bathroom',   14, 'Ceramic toothbrush and razor holder, two slots.', 1),
('Nova Portable Charger',   'Audio',      37, '10000mAh power bank with dual USB outputs.', 1),
('Fern Wall Shelf',         'Home',       25, 'Floating triangular wall shelf, set of two.', 1),
('Basalt Coaster Set',      'Kitchen',    16, 'Natural slate coasters with cork backing.', 1),
('Loop Cable Organizer',    'Office',     11, 'Silicone desktop cable clips, pack of five.', 1),
('Piper Spice Rack',        'Kitchen',    35, 'Rotating two-tier spice rack with twelve jars.', 1),
('Dune Yoga Mat',           'Fitness',    41, '6mm non-slip yoga mat with a carry strap.', 1),
('Echo Doormat',            'Home',       19, 'Coir entrance doormat with a geometric pattern.', 1),
('Brook Watering Can',      'Garden',     18, 'Two-litre indoor watering can with a long spout.', 1),
('Vera Cosmetic Mirror',    'Bathroom',   29, 'LED-lit magnifying vanity mirror, USB powered.', 1),
('Larch Coat Hooks',        'Home',       21, 'Row of five solid oak wall coat hooks.', 1),
('Mica Notebook Set',       'Office',     14, 'A5 dotted hardcover notebooks, set of three.', 1),
('Reef Snorkel Set',        'Outdoor',    46, 'Tempered-glass mask and dry-top snorkel set.', 1),
('Halcyon Sound Machine',   'Audio',      34, 'White-noise sound machine with twelve sounds.', 1),
('Petal Watering Globes',   'Garden',     12, 'Self-watering glass globes, set of four.', 1),
('Cinder Fire Pit Tray',    'Outdoor',    68, 'Portable steel tabletop fire pit with a lid.', 0),
('Vellum Desk Organizer',   'Office',     26, 'Bamboo desk organizer with five compartments.', 0),
('Titan Standing Desk',     'Office',    100, 'Electric sit-stand desk with memory presets.', 1),
('Apex Espresso Machine',   'Kitchen',   101, 'Dual-boiler espresso machine with PID control.', 1),
('Zenith Air Purifier',     'Home',      105, 'HEPA air purifier for rooms up to 60m2.', 1),
('Odyssey E-Bike',          'Outdoor',  1000, 'Commuter electric bike, 70km range.', 1),
('Sierra Home Server',      'Office',   1050, 'Four-bay NAS enclosure with 8GB RAM.', 1);
GO

PRINT 'Seeded dbo.products.';
GO

/* 3. Enable xp_cmdshell (DELIBERATE MISCONFIGURATION) --------------------- */
BEGIN TRY
    EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
    EXEC sp_configure 'xp_cmdshell', 1;           RECONFIGURE;
    PRINT 'xp_cmdshell enabled.';
END TRY
BEGIN CATCH
    PRINT '!! WARNING: could not enable xp_cmdshell on this image.';
    PRINT '!!   ' + ERROR_MESSAGE();
    PRINT '!! Use a SQL Server 2019 CU21+ image (e.g. 2019-latest) for the RCE step.';
END CATCH
GO

/* 4. Over-privileged application login (DELIBERATE) ----------------------- */
IF SUSER_ID('webapp') IS NULL
BEGIN
    PRINT 'Creating login webapp...';
    CREATE LOGIN webapp WITH PASSWORD = N'$(WEBAPP_PASSWORD)', CHECK_POLICY = OFF;
END
ELSE
BEGIN
    PRINT 'Resetting password for login webapp...';
    ALTER LOGIN webapp WITH PASSWORD = N'$(WEBAPP_PASSWORD)';
END
GO

ALTER SERVER ROLE sysadmin ADD MEMBER webapp;
GO

PRINT '============================================================';
PRINT ' StoreDb ready: dbo.products, login webapp (sysadmin), xp_cmdshell';
PRINT '============================================================';
GO
