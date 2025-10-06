SELECT * from MinPerpEquityForOrders as minO
LEFT JOIN Orders as o ON minO.OrderId = o.OrderId
ORDER BY Wallet, MinPE DESC