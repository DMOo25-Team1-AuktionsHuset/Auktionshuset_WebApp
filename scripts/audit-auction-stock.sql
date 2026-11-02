-- Read-only pre-rollout audit. Run against the intended database; never apply automatic corrections.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

-- Active open lines are reservations, including auctions past their planned end.
WITH reservations AS (
    SELECT al."LotId", SUM(al."Quantity"::bigint) AS reserved
    FROM "AuctionLot" al JOIN "Auction" a ON a."AuctionId" = al."AuctionId"
    WHERE al."ClosedAtTime" IS NULL AND a."ClosedAtTime" IS NULL
      AND a."AuctionStatus" IN ('Kommende', 'Live')
    GROUP BY al."LotId"
)
SELECT l."LotId", l."Name", l."Quantity" AS physical_stock,
       COALESCE(r.reserved, 0) AS reserved,
       l."Quantity" - COALESCE(r.reserved, 0) AS available
FROM "Lot" l LEFT JOIN reservations r ON r."LotId" = l."LotId"
WHERE l."Quantity" < 0 OR COALESCE(r.reserved, 0) > l."Quantity";

-- Never round prices or change unit counts as a repair.
SELECT "AuctionLotId", "AuctionId", "LotId", "Quantity", "StartingPrice"
FROM "AuctionLot"
WHERE "Quantity" <= 0 OR "StartingPrice" < 0
   OR "StartingPrice" > 9999999999999999.99
   OR "StartingPrice" <> ROUND("StartingPrice", 2);

-- Legacy/unknown statuses and inconsistent lifecycle markers require explicit reconciliation.
SELECT "AuctionId", "AuctionStatus", "StartedAtTime", "ClosedAtTime"
FROM "Auction"
WHERE "AuctionStatus" NOT IN ('Kommende', 'Live', 'Afsluttet')
   OR ("AuctionStatus" = 'Live' AND ("StartedAtTime" IS NULL OR "ClosedAtTime" IS NOT NULL))
   OR ("AuctionStatus" = 'Kommende' AND ("StartedAtTime" IS NOT NULL OR "ClosedAtTime" IS NOT NULL))
   OR ("AuctionStatus" = 'Afsluttet' AND "ClosedAtTime" IS NULL);

-- Historical closed sales: informational reconciliation list, NOT instructions to debit again.
-- ClosedAtTime is the idempotency boundary; old closed rows will never be debited by this release.
SELECT al."AuctionLotId", al."AuctionId", al."LotId", al."Quantity", al."ClosedAtTime",
       al."CurrentHighestBidId", l."Quantity" AS current_physical_stock
FROM "AuctionLot" al JOIN "Lot" l ON l."LotId" = al."LotId"
WHERE al."ClosedAtTime" IS NOT NULL
  AND (al."CurrentHighestBidId" IS NOT NULL
       OR EXISTS (SELECT 1 FROM "Bids" b WHERE b."AuctionLotId" = al."AuctionLotId"));

COMMIT;
