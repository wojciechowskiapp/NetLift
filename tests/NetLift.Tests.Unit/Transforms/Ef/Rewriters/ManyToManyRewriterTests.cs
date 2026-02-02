using FluentAssertions;
using NetLift.Transforms.Ef.Rewriters;

namespace NetLift.Tests.Unit.Transforms.Ef.Rewriters;

public sealed class ManyToManyRewriterTests
{
    private readonly ManyToManyRewriter _rewriter = new();

    [Fact]
    public void Rewrite_SimpleManyToMany_NoChangeNeeded()
    {
        // Arrange
        var sourceCode = @"
using Microsoft.EntityFrameworkCore;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students);
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Be(sourceCode);
        _rewriter.ConfidenceScore.Should().Be(95);
        _rewriter.DetectedRelationships.Should().HaveCount(1);
        _rewriter.DetectedRelationships.First().LeftEntity.Should().Be("Student");
        _rewriter.DetectedRelationships.First().RightEntity.Should().Be("Course"); // Inferred from Courses navigation property
        _rewriter.DetectedRelationships.First().JoinTableName.Should().BeNull();
        _rewriter.Diagnostics.Should().ContainSingle();
    }

    [Fact]
    public void Rewrite_WithMapAndTableName_TransformsToUsingEntity()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(m => m.ToTable(""StudentCourses""));
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("UsingEntity");
        result.Should().Contain("Dictionary<string, object>");
        result.Should().Contain("\"StudentCourses\"");
        _rewriter.ConfidenceScore.Should().Be(80);
        _rewriter.RequiredUsings.Should().Contain("System.Collections.Generic");
    }

    [Fact]
    public void Rewrite_WithMapAndKeyNames_TransformsToUsingEntity()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(cs =>
            {
                cs.MapLeftKey(""StudentId"");
                cs.MapRightKey(""CourseId"");
            });
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("UsingEntity");
        result.Should().Contain("\"StudentId\"");
        result.Should().Contain("\"CourseId\"");
        _rewriter.ConfidenceScore.Should().Be(80);
    }

    [Fact]
    public void Rewrite_WithFullMapConfiguration_TransformsToUsingEntity()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(cs =>
            {
                cs.MapLeftKey(""StudentId"");
                cs.MapRightKey(""CourseId"");
                cs.ToTable(""StudentCourses"");
            });
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("UsingEntity");
        result.Should().Contain("\"StudentCourses\"");
        result.Should().Contain("\"StudentId\"");
        result.Should().Contain("\"CourseId\"");
        result.Should().Contain("HasOne<");
        result.Should().Contain("WithMany()");
        result.Should().Contain("HasForeignKey");
        _rewriter.ConfidenceScore.Should().Be(80);
        _rewriter.DetectedRelationships.Should().HaveCount(1);
        _rewriter.DetectedRelationships.First().JoinTableName.Should().Be("StudentCourses");
        _rewriter.DetectedRelationships.First().LeftKeyName.Should().Be("StudentId");
        _rewriter.DetectedRelationships.First().RightKeyName.Should().Be("CourseId");
    }

    [Fact]
    public void Rewrite_ComplexMapConfiguration_GeneratesTodoComment()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(cs =>
            {
                cs.MapLeftKey(""StudentId"");
                cs.MapRightKey(""CourseId"");
                cs.ToTable(""StudentCourses"");
                cs.HasColumnAnnotation(""CreatedDate"", ""Index"", new IndexAnnotation(new IndexAttribute()));
            });
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("TODO");
        result.Should().Contain("complex");
        _rewriter.ConfidenceScore.Should().Be(65);
        _rewriter.Diagnostics.Should().Contain(d => d.Severity == NetLift.Core.Interfaces.RewriterDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Rewrite_MultipleManyToMany_DetectsAll()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students);

        modelBuilder.Entity<Teacher>()
            .HasMany(t => t.Courses)
            .WithMany(c => c.Teachers)
            .Map(m => m.ToTable(""TeacherCourses""));
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        _rewriter.DetectedRelationships.Should().HaveCount(2);
        _rewriter.DetectedRelationships.Should().Contain(r => r.LeftEntity == "Student");
        _rewriter.DetectedRelationships.Should().Contain(r => r.LeftEntity == "Teacher");
    }

    [Fact]
    public void Rewrite_NonEfCode_NoChanges()
    {
        // Arrange
        var sourceCode = @"
public class MyClass
{
    public void MyMethod()
    {
        var result = collection.HasMany(x => x.Items);
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Be(sourceCode);
        _rewriter.DetectedRelationships.Should().BeEmpty();
        _rewriter.ConfidenceScore.Should().Be(100); // No changes
    }

    [Fact]
    public void Rewrite_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var sourceCode = "";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void Rewrite_NullString_ReturnsNull()
    {
        // Arrange
        string sourceCode = null!;

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Rewrite_OnlyHasMany_NoTransformation()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Enrollments);
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Be(sourceCode);
        _rewriter.DetectedRelationships.Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_PreservesFormatting()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        // Configure many-to-many
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students);
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        result.Should().Contain("// Configure many-to-many");
        result.Should().Contain("protected override void OnModelCreating");
    }

    [Fact]
    public void DetectedRelationships_ContainsCorrectInfo()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(cs =>
            {
                cs.MapLeftKey(""StudentRefId"");
                cs.MapRightKey(""CourseRefId"");
                cs.ToTable(""Enrollments"");
            });
    }
}";

        // Act
        _rewriter.Rewrite(sourceCode);

        // Assert
        var relationship = _rewriter.DetectedRelationships.Should().ContainSingle().Subject;
        relationship.LeftEntity.Should().Be("Student");
        relationship.JoinTableName.Should().Be("Enrollments");
        relationship.LeftKeyName.Should().Be("StudentRefId");
        relationship.RightKeyName.Should().Be("CourseRefId");
    }

    [Fact]
    public void Diagnostics_ContainsTransformationInfo()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(m => m.ToTable(""StudentCourses""));
    }
}";

        // Act
        _rewriter.Rewrite(sourceCode);

        // Assert
        _rewriter.Diagnostics.Should().NotBeEmpty();
        _rewriter.Diagnostics.Should().Contain(d =>
            d.Message.Contains("Transforming") || d.Message.Contains("UsingEntity"));
    }

    [Fact]
    public void Rewrite_EmptyMap_RemovesMapCall()
    {
        // Arrange
        var sourceCode = @"
using System.Data.Entity;

public class SchoolContext : DbContext
{
    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>()
            .HasMany(s => s.Courses)
            .WithMany(c => c.Students)
            .Map(m => { });
    }
}";

        // Act
        var result = _rewriter.Rewrite(sourceCode);

        // Assert
        // Empty Map should be removed or left as-is with high confidence
        _rewriter.ConfidenceScore.Should().BeGreaterOrEqualTo(80);
    }
}
