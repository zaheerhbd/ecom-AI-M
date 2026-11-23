-- LOCAL DEV ONLY: one paid order for the seeded test user bob@test.com, so the order tools
-- (get_my_orders, get_order) and the multi-turn eval OR-004 have real data to read.
-- Never run this against the cloud database.
-- Run:  docker exec -i ecom-ai-m-db-1 psql -U appuser -d skinet < docs/agentic/dev/seed-test-order.sql
-- Safe to run twice: it does nothing if bob already has an order.
INSERT INTO "Orders" ("BuyerEmail","OrderDate","ShipToAddress_FirstName","ShipToAddress_LastName",
  "ShipToAddress_Street","ShipToAddress_City","ShipToAddress_State","ShipToAddress_ZipCode",
  "DeliveryMethodId","Subtotal","Status","PaymentIntentId")
SELECT 'bob@test.com', TIMESTAMPTZ '2026-11-18 10:30:00+00', 'Bob', 'Bobbity',
  '10 The Street', 'New York', 'NY', '90250', 2, 208.00, 'PaymentReceived', NULL
WHERE NOT EXISTS (SELECT 1 FROM "Orders" WHERE "BuyerEmail" = 'bob@test.com');

INSERT INTO "OrderItems" ("ItemOrdered_ProductItemId","ItemOrdered_ProductName","ItemOrdered_PictureUrl","Price","Quantity","OrderId")
SELECT p."Id", p."Name", p."PictureUrl", p."Price", 1, o."Id"
FROM "Orders" o
JOIN "Products" p ON p."Id" IN (1, 8)
WHERE o."BuyerEmail" = 'bob@test.com'
  AND NOT EXISTS (SELECT 1 FROM "OrderItems" i WHERE i."OrderId" = o."Id");
