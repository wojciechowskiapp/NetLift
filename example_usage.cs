// TASK-048: SqlQueryRewriter Implementation Example
// This file demonstrates the SqlQueryRewriter transformation capabilities

using NetLift.Transforms.Ef.Rewriters;

var rewriter = new SqlQueryRewriter();

// Example 1: Simple SqlQuery with placeholders
var ef6Code1 = @"
var products = Database.SqlQuery<Product>(
    ""SELECT * FROM Products WHERE CategoryId = @p0"", categoryId).ToList();
";

var efCoreCode1 = rewriter.Rewrite(ef6Code1);
// Result: Products.FromSqlRaw("SELECT * FROM Products WHERE CategoryId = {0}", categoryId).ToList();

// Example 2: Interpolated string
var ef6Code2 = @"
var products = Database.SqlQuery<Product>(
    $""SELECT * FROM Products WHERE Price > {minPrice}"").ToList();
";

var efCoreCode2 = rewriter.Rewrite(ef6Code2);
// Result: Products.FromSqlInterpolated($"SELECT * FROM Products WHERE Price > {minPrice}").ToList();

// Example 3: ExecuteSqlCommand
var ef6Code3 = @"
Database.ExecuteSqlCommand(
    ""UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = @p0"", categoryId);
";

var efCoreCode3 = rewriter.Rewrite(ef6Code3);
// Result: Database.ExecuteSqlRaw("UPDATE Products SET Price = Price * 1.1 WHERE CategoryId = {0}", categoryId);

// Example 4: Keyless entity
var ef6Code4 = @"
var summaries = Database.SqlQuery<ProductSummary>(
    ""SELECT CategoryId, COUNT(*) AS Count FROM Products GROUP BY CategoryId"").ToList();
";

var efCoreCode4 = rewriter.Rewrite(ef6Code4);
// Result: Set<ProductSummary>().FromSqlRaw("SELECT CategoryId, COUNT(*) AS Count FROM Products GROUP BY CategoryId").ToList();
// Plus warning: ProductSummary needs keyless entity configuration

// Check diagnostics and confidence
Console.WriteLine($"Confidence: {rewriter.ConfidenceScore}%");
Console.WriteLine($"Diagnostics: {rewriter.Diagnostics.Count}");
Console.WriteLine($"Keyless types: {string.Join(", ", rewriter.KeylessTypesDetected)}");
