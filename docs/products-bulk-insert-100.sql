-- Inserts 100 sample catalog products into the PostgreSQL "Products" table.
-- Assumes ProductTypeId values:
--   1 = Boards, 2 = Hats, 3 = Boots, 4 = Gloves
-- Assumes ProductBrandId values:
--   1 = Angular, 2 = NetCore, 3 = VS Code, 4 = React, 5 = Typescript, 6 = Redis

BEGIN;

INSERT INTO "Products" ("Name", "Description", "Price", "PictureUrl", "ProductTypeId", "ProductBrandId")
SELECT
    format('%s %s %s %s',
        brand_name,
        adjective,
        product_label,
        lpad(n::text, 3, '0')) AS "Name",
    format('%s %s %s built for ecommerce demo catalog item %s with durable materials and clean product styling.',
        brand_name,
        adjective,
        lower(product_label),
        lpad(n::text, 3, '0')) AS "Description",
    price AS "Price",
    format('images/products/catalog-%s-%s.png', lpad(n::text, 3, '0'), brand_slug) AS "PictureUrl",
    product_type_id AS "ProductTypeId",
    product_brand_id AS "ProductBrandId"
FROM (
    SELECT
        gs AS n,
        ((gs - 1) % 6) + 1 AS product_brand_id,
        CASE
            WHEN gs BETWEEN 1 AND 25 THEN 1
            WHEN gs BETWEEN 26 AND 50 THEN 2
            WHEN gs BETWEEN 51 AND 75 THEN 3
            ELSE 4
        END AS product_type_id,
        (ARRAY['Angular', 'NetCore', 'VS Code', 'React', 'Typescript', 'Redis'])[ ((gs - 1) % 6) + 1 ] AS brand_name,
        (ARRAY['angular', 'netcore', 'vscode', 'react', 'typescript', 'redis'])[ ((gs - 1) % 6) + 1 ] AS brand_slug,
        (ARRAY['Velocity', 'Summit', 'Pulse', 'Forge', 'Drift', 'Nova', 'Apex', 'Stride', 'Echo', 'Vertex'])[ ((gs - 1) % 10) + 1 ] AS adjective,
        CASE
            WHEN gs BETWEEN 1 AND 25 THEN 'Board'
            WHEN gs BETWEEN 26 AND 50 THEN 'Hat'
            WHEN gs BETWEEN 51 AND 75 THEN 'Boot'
            ELSE 'Glove'
        END AS product_label,
        CASE
            WHEN gs BETWEEN 1 AND 25 THEN round((160 + (gs * 6.5))::numeric, 2)
            WHEN gs BETWEEN 26 AND 50 THEN round((18 + ((gs - 25) * 1.8))::numeric, 2)
            WHEN gs BETWEEN 51 AND 75 THEN round((75 + ((gs - 50) * 4.25))::numeric, 2)
            ELSE round((22 + ((gs - 75) * 2.15))::numeric, 2)
        END AS price
    FROM generate_series(1, 100) AS gs
) AS generated_products;

COMMIT;
