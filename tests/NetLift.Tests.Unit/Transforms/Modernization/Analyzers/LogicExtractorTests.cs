using FluentAssertions;
using NetLift.Core.Models.Modernization;
using NetLift.Transforms.Modernization.Analyzers;

namespace NetLift.Tests.Unit.Transforms.Modernization.Analyzers;

public sealed class LogicExtractorTests
{
    private readonly LogicExtractor _extractor = new();

    #region Basic Extraction

    [Fact]
    public async Task ExtractAsync_UsingExtractFromMethodAsync_Works()
    {
        // Arrange - full method declaration
        var methodSource = @"
public void TestMethod()
{
    var item = 1;
    var name = ""test"";
}";

        // Act
        var result = await _extractor.ExtractFromMethodAsync(methodSource);

        // Assert
        result.Should().NotBeNull();
        result.Confidence.Should().BeGreaterThan(0, "valid method should have non-zero confidence");
        result.Variables.Should().NotBeEmpty("variable declarations should be extracted");
    }

    [Fact]
    public async Task ExtractAsync_WithDbQuery_ExtractsDbOperations()
    {
        // Arrange
        var methodBody = @"var items = db.Items.Where(x => x.Active).ToList();";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.Should().NotBeNull();
        result.Statements.Should().NotBeEmpty();
        result.DbOperations.Should().NotBeEmpty("LINQ query on db should be detected");
    }

    [Fact]
    public async Task ExtractAsync_WithReturn_ExtractsReturnStatement()
    {
        // Arrange - Using ExtractFromMethodAsync for full method
        var methodSource = @"public ActionResult Index()
{
    var items = db.Items.ToList();
    return View(items);
}";

        // Act
        var result = await _extractor.ExtractFromMethodAsync(methodSource);

        // Assert
        result.Should().NotBeNull();
        result.ReturnStatement.Should().NotBeNull();
        result.ReturnStatement!.IsViewReturn.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_EmptyBody_ReturnsEmptyLogic()
    {
        // Arrange
        var methodBody = "";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.Statements.Should().BeEmpty();
        result.Confidence.Should().Be(0);
    }

    #endregion

    #region LINQ Query Syntax Detection

    [Fact]
    public async Task ExtractAsync_LinqQuerySyntax_DetectsDbOperation()
    {
        // Arrange - LINQ query syntax like in HomeController.About()
        var methodBody = @"
            IQueryable<EnrollmentDataGroup> data =
                from student in db.Students
                group student by student.EnrollmentDate into dataGroup
                select new EnrollmentDataGroup() { EnrollmentDate = dataGroup.Key, StudentCount = dataGroup.Count() };
            return View(data.ToList());";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.DbOperations.Should().NotBeEmpty();
        result.DbOperations.Should().Contain(op => op.LinqOperations.Contains("GroupBy"));
        result.DbOperations.Should().Contain(op => op.LinqOperations.Contains("Select"));
    }

    [Fact]
    public async Task ExtractAsync_LinqQueryWithJoin_DetectsOperation()
    {
        // Arrange
        var methodBody = @"
            var result = from c in db.Courses
                         join d in db.Departments on c.DepartmentID equals d.ID
                         select new { c.Title, d.Name };
            return View(result.ToList());";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.DbOperations.Should().Contain(op => op.LinqOperations.Contains("Join"));
    }

    #endregion

    #region Explicit Load Detection

    [Fact]
    public async Task ExtractAsync_ExplicitCollectionLoad_DetectsPattern()
    {
        // Arrange - Explicit loading pattern like in InstructorController.Index()
        var methodBody = @"
            var course = db.Courses.Single(c => c.ID == id);
            db.Entry(course).Collection(x => x.Enrollments).Load();
            return View(course);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ExplicitLoads.Should().NotBeEmpty();
        result.ExplicitLoads.Should().Contain(el =>
            el.EntityVariable == "course" &&
            el.NavigationProperty == "Enrollments" &&
            el.IsCollection == true);
        result.Warnings.Should().Contain(w => w.Contains("Explicit loading"));
    }

    [Fact]
    public async Task ExtractAsync_ExplicitReferenceLoad_DetectsPattern()
    {
        // Arrange
        var methodBody = @"
            var enrollment = db.Enrollments.Single(e => e.ID == id);
            db.Entry(enrollment).Reference(x => x.Student).Load();
            return View(enrollment);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ExplicitLoads.Should().NotBeEmpty();
        result.ExplicitLoads.Should().Contain(el =>
            el.EntityVariable == "enrollment" &&
            el.NavigationProperty == "Student" &&
            el.IsCollection == false);
    }

    [Fact]
    public async Task ExtractAsync_MultipleExplicitLoads_DetectsAll()
    {
        // Arrange - Like InstructorController with nested explicit loads
        var methodBody = @"
            var course = db.Courses.Single();
            db.Entry(course).Collection(x => x.Enrollments).Load();
            foreach (var enrollment in course.Enrollments)
            {
                db.Entry(enrollment).Reference(x => x.Student).Load();
            }
            return View(course);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ExplicitLoads.Should().HaveCountGreaterOrEqualTo(2);
        result.ExplicitLoads.Should().Contain(el => el.NavigationProperty == "Enrollments");
        result.ExplicitLoads.Should().Contain(el => el.NavigationProperty == "Student");
    }

    #endregion

    #region ViewModel Mutation Detection

    [Fact]
    public async Task ExtractAsync_ViewModelMutation_DetectsPropertyAssignment()
    {
        // Arrange - Use a clearer variable name pattern
        var methodBody = @"
            var model = new InstructorIndexData();
            model.Instructors = db.Instructors.ToList();
            model.Courses = new List<Course>();
            return View(model);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - Check that statements are captured
        result.Statements.Should().NotBeEmpty();
        // The variable 'model' should be tracked
        result.Variables.Should().Contain(v => v.Name == "model");
        // Assignment statements should be recorded
        result.Statements.Should().Contain(s => s.Type == StatementType.Assignment);
    }

    [Fact]
    public async Task ExtractAsync_ViewBagAssignment_DetectsAsMutation()
    {
        // Arrange
        var methodBody = @"
            ViewBag.InstructorID = id.Value;
            ViewBag.Message = ""Hello"";
            return View();";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.HasViewBagUsage.Should().BeTrue();
        result.ViewModelMutations.Should().Contain(m =>
            m.ViewModelVariable == "ViewBag" && m.PropertyName == "InstructorID");
        result.ViewModelMutations.Should().Contain(m =>
            m.ViewModelVariable == "ViewBag" && m.PropertyName == "Message");
    }

    #endregion

    #region Conditional Block Detection

    [Fact]
    public async Task ExtractAsync_ConditionalNullCheck_CreatesConditionalBlock()
    {
        // Arrange - Like InstructorController.Index()
        var methodBody = @"
            var viewModel = new InstructorIndexData();
            viewModel.Instructors = db.Instructors.ToList();
            if (id != null)
            {
                ViewBag.InstructorID = id.Value;
                viewModel.Courses = viewModel.Instructors.Where(i => i.ID == id.Value).Single().Courses;
            }
            return View(viewModel);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ConditionalBlocks.Should().NotBeEmpty();
        result.ConditionalBlocks.Should().Contain(cb =>
            cb.ParameterName == "id" &&
            cb.Condition.Contains("id != null"));
    }

    [Fact]
    public async Task ExtractAsync_MultipleConditionalBlocks_DetectsAll()
    {
        // Arrange
        var methodBody = @"
            var viewModel = new IndexData();
            if (id != null)
            {
                viewModel.Items = db.Items.Where(i => i.ID == id).ToList();
            }
            if (categoryId != null)
            {
                viewModel.Category = db.Categories.Find(categoryId.Value);
            }
            return View(viewModel);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ConditionalBlocks.Should().HaveCount(2);
        result.ConditionalBlocks.Should().Contain(cb => cb.ParameterName == "id");
        result.ConditionalBlocks.Should().Contain(cb => cb.ParameterName == "categoryId");
    }

    [Fact]
    public async Task ExtractAsync_HasValueCondition_ExtractsParameterName()
    {
        // Arrange
        var methodBody = @"
            if (id.HasValue)
            {
                var item = db.Items.Find(id.Value);
            }
            return View();";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ConditionalBlocks.Should().NotBeEmpty();
        result.ConditionalBlocks.Should().Contain(cb =>
            cb.ParameterName == "id" &&
            cb.Condition.Contains("HasValue"));
    }

    [Fact]
    public async Task ExtractAsync_ConditionalWithMutations_TracksMutationsAsConditional()
    {
        // Arrange
        var methodBody = @"
            var viewModel = new IndexData();
            if (id != null)
            {
                viewModel.SelectedId = id.Value;
                viewModel.Details = db.Items.Find(id.Value);
            }
            return View(viewModel);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.ConditionalBlocks.Should().NotBeEmpty();
        var conditionalBlock = result.ConditionalBlocks.First(cb => cb.ParameterName == "id");
        conditionalBlock.Mutations.Should().NotBeEmpty();
        conditionalBlock.Mutations.Should().OnlyContain(m => m.IsConditional);
    }

    #endregion

    #region Complex Controller Pattern

    [Fact]
    public async Task ExtractAsync_ComplexInstructorIndexPattern_ExtractsAllPatterns()
    {
        // Arrange - Full InstructorController.Index() pattern
        var methodBody = @"
            var viewModel = new InstructorIndexData();
            viewModel.Instructors = db.Instructors
                .Include(i => i.OfficeAssignment)
                .Include(i => i.Courses.Select(c => c.Department))
                .OrderBy(i => i.LastName);

            if (id != null)
            {
                ViewBag.InstructorID = id.Value;
                viewModel.Courses = viewModel.Instructors.Where(i => i.ID == id.Value).Single().Courses;
            }

            if (courseID != null)
            {
                ViewBag.CourseID = courseID.Value;
                var selectedCourse = viewModel.Courses.Where(x => x.CourseID == courseID).Single();
                db.Entry(selectedCourse).Collection(x => x.Enrollments).Load();
                foreach (var enrollment in selectedCourse.Enrollments)
                {
                    db.Entry(enrollment).Reference(x => x.Student).Load();
                }
                viewModel.Enrollments = selectedCourse.Enrollments;
            }

            return View(viewModel);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        // ViewBag usage detected
        result.HasViewBagUsage.Should().BeTrue();

        // Conditional blocks for both parameters
        result.ConditionalBlocks.Should().HaveCount(2);
        result.ConditionalBlocks.Should().Contain(cb => cb.ParameterName == "id");
        result.ConditionalBlocks.Should().Contain(cb => cb.ParameterName == "courseID");

        // Explicit loads detected
        result.ExplicitLoads.Should().NotBeEmpty();
        result.ExplicitLoads.Should().Contain(el => el.NavigationProperty == "Enrollments");
        result.ExplicitLoads.Should().Contain(el => el.NavigationProperty == "Student");

        // View model mutations
        result.ViewModelMutations.Should().Contain(m => m.PropertyName == "Instructors");
        result.ViewModelMutations.Should().Contain(m => m.PropertyName == "Courses");
        result.ViewModelMutations.Should().Contain(m => m.PropertyName == "Enrollments");

        // Confidence should be lower due to complexity
        result.Confidence.Should().BeLessThan(90);
    }

    #endregion

    #region Async Transformation

    [Fact]
    public void TransformToAsync_SyncMethods_TransformedCorrectly()
    {
        // Arrange
        var logic = new ExtractedLogic
        {
            Statements = new List<StatementInfo>
            {
                new StatementInfo
                {
                    Type = StatementType.Declaration,
                    SourceCode = "var items = db.Items.ToList();",
                    NeedsAsyncTransform = true
                }
            },
            Variables = new List<VariableInfo>(),
            ServiceCalls = new List<MethodCallInfo>(),
            DbOperations = new List<DbContextOperation>(),
            ExplicitLoads = new List<ExplicitLoadOperation>(),
            ViewModelMutations = new List<ViewModelMutation>(),
            ConditionalBlocks = new List<ConditionalBlock>(),
            UsedDependencies = new List<string>(),
            Warnings = new List<string>(),
            Confidence = 100
        };

        // Act
        var result = _extractor.TransformToAsync(logic);

        // Assert
        result.Statements.Should().Contain(s =>
            s.TransformedCode != null &&
            s.TransformedCode.Contains("ToListAsync"));
    }

    #endregion

    #region Confidence Scoring

    [Fact]
    public async Task ExtractAsync_SimpleQuery_HighConfidence()
    {
        // Arrange
        var methodBody = @"
            var items = db.Items.ToList();
            return View(items);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.Confidence.Should().BeGreaterOrEqualTo(85);
    }

    [Fact]
    public async Task ExtractAsync_WithViewBag_LowerConfidence()
    {
        // Arrange
        var methodBody = @"
            ViewBag.Message = ""Hello"";
            return View();";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.Confidence.Should().BeLessThanOrEqualTo(75);
    }

    [Fact]
    public async Task ExtractAsync_WithExplicitLoads_LowerConfidence()
    {
        // Arrange
        var methodBody = @"
            var course = db.Courses.First();
            db.Entry(course).Collection(x => x.Enrollments).Load();
            return View(course);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        result.Confidence.Should().BeLessThanOrEqualTo(70);
    }

    #endregion

    #region Multiple Return Statements

    [Fact]
    public async Task ExtractAsync_MultipleEarlyReturns_OnlyFinalReturnInTopLevel()
    {
        // Arrange - multiple early returns in if statements
        var methodBody = @"
            if (id == null) return BadRequest();
            var item = db.Items.Find(id);
            if (item == null) return NotFound();
            return View(item);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - only the final unconditional return should be in top-level Statements
        var topLevelReturns = result.Statements.Count(s => s.Type == StatementType.Return);
        topLevelReturns.Should().Be(1, "only the final unconditional return should be in top-level Statements");

        // The final return should be in ReturnStatement
        result.ReturnStatement.Should().NotBeNull();
        result.ReturnStatement!.Expression.Should().Contain("View(item)");

        // Early returns should only be in ChildStatements of if blocks
        var ifStatements = result.Statements.Where(s => s.Type == StatementType.If).ToList();
        ifStatements.Should().HaveCount(2, "there are two if statements");

        // Each if statement should have a return in its ChildStatements
        foreach (var ifStmt in ifStatements)
        {
            ifStmt.ChildStatements.Should().Contain(
                cs => cs.Type == StatementType.Return,
                "early returns should be in ChildStatements of if blocks");
        }
    }

    [Fact]
    public async Task ExtractAsync_EarlyReturnInIfElse_NotDuplicatedInTopLevel()
    {
        // Arrange - early returns in if-else
        var methodBody = @"
            if (condition)
            {
                return View(""Success"");
            }
            else
            {
                return View(""Error"");
            }";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - no returns in top-level Statements, all are inside if/else
        var topLevelReturns = result.Statements.Count(s => s.Type == StatementType.Return);
        topLevelReturns.Should().Be(0, "returns inside if-else blocks should not be in top-level Statements");

        // Returns should be in ChildStatements of if and else
        var ifStmt = result.Statements.FirstOrDefault(s => s.Type == StatementType.If);
        ifStmt.Should().NotBeNull();
        ifStmt!.ChildStatements.Should().Contain(cs => cs.Type == StatementType.Return);

        var elseStmt = result.Statements.FirstOrDefault(s => s.Type == StatementType.Else);
        elseStmt.Should().NotBeNull();
        elseStmt!.ChildStatements.Should().Contain(cs => cs.Type == StatementType.Return);
    }

    [Fact]
    public async Task ExtractAsync_OnlyUnconditionalReturn_InTopLevel()
    {
        // Arrange - only unconditional return
        var methodBody = @"
            var items = db.Items.ToList();
            return View(items);";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - the unconditional return should be in top-level Statements
        var topLevelReturns = result.Statements.Count(s => s.Type == StatementType.Return);
        topLevelReturns.Should().Be(1, "unconditional return should be in top-level Statements");

        result.ReturnStatement.Should().NotBeNull();
        result.ReturnStatement!.Expression.Should().Contain("View(items)");
    }

    #endregion

    #region Switch Statement Handling

    [Fact]
    public async Task ExtractAsync_SwitchStatement_PreservesEntireBlock()
    {
        // Arrange - switch statement with multiple cases
        var methodBody = @"
            var status = ""active"";
            switch (status)
            {
                case ""active"":
                    var activeItems = db.Items.Where(x => x.Active).ToList();
                    break;
                case ""inactive"":
                    var inactiveItems = db.Items.Where(x => !x.Active).ToList();
                    break;
                default:
                    var allItems = db.Items.ToList();
                    break;
            }
            return View();";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - switch should be preserved as single statement
        var switchStatements = result.Statements.Where(s => s.Type == StatementType.Switch).ToList();
        switchStatements.Should().HaveCount(1, "switch statement should be captured as single statement");

        var switchStmt = switchStatements[0];
        switchStmt.SourceCode.Should().Contain("switch (status)");
        switchStmt.SourceCode.Should().Contain("case \"active\":");
        switchStmt.SourceCode.Should().Contain("case \"inactive\":");
        switchStmt.SourceCode.Should().Contain("default:");
        switchStmt.SourceCode.Should().Contain("break;");

        result.Warnings.Should().Contain(w => w.Contains("Switch statement found - preserving as-is"));
    }

    [Fact]
    public async Task ExtractAsync_SwitchStatement_DoesNotExtractCaseBodiesIndividually()
    {
        // Arrange - switch statement
        var methodBody = @"
            switch (status)
            {
                case ""active"":
                    var activeItems = db.Items.Where(x => x.Active).ToList();
                    break;
                case ""inactive"":
                    var inactiveItems = db.Items.Where(x => !x.Active).ToList();
                    break;
            }";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert - should NOT have separate Declaration statements for variables inside cases
        var declarationStatements = result.Statements.Where(s => s.Type == StatementType.Declaration).ToList();
        declarationStatements.Should().BeEmpty("case body statements should not be extracted individually");

        // Should only have the switch statement itself
        result.Statements.Should().HaveCount(1);
        result.Statements[0].Type.Should().Be(StatementType.Switch);
    }

    [Fact]
    public async Task ExtractAsync_SwitchWithAsyncCalls_FlagsNeedsAsyncTransform()
    {
        // Arrange - switch with async EF calls
        var methodBody = @"
            switch (operation)
            {
                case ""list"":
                    var items = db.Items.ToList();
                    break;
                case ""find"":
                    var item = db.Items.Find(id);
                    break;
            }";

        // Act
        var result = await _extractor.ExtractAsync(methodBody);

        // Assert
        var switchStmt = result.Statements.FirstOrDefault(s => s.Type == StatementType.Switch);
        switchStmt.Should().NotBeNull();
        switchStmt!.NeedsAsyncTransform.Should().BeTrue("switch contains ToList() and Find() which need async");
    }

    #endregion
}
