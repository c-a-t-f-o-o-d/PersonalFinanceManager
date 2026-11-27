SELECT COUNT(*) FROM dbo.TransactionLink AS link WHERE link.LinkType = N'ManualReview' AND link.Notes LIKE N'Review required after upgrade: %';
