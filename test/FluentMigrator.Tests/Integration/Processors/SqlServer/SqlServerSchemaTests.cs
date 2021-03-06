using System.Data.SqlClient;
using FluentMigrator.Runner.Announcers;
using FluentMigrator.Runner.Generators.SqlServer;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.Processors.SqlServer;
using NUnit.Framework;
using NUnit.Should;

namespace FluentMigrator.Tests.Integration.Processors.SqlServer
{
    [TestFixture]
    [Category("Integration")]
    [Category("SqlServer2012")]
    public class SqlServerSchemaTests : BaseSchemaTests
    {
        public SqlConnection Connection { get; set; }
        public SqlServerProcessor Processor { get; set; }

        [SetUp]
        public void SetUp()
        {
            if (!IntegrationTestOptions.SqlServer2012.IsEnabled)
                Assert.Ignore();
            Connection = new SqlConnection(IntegrationTestOptions.SqlServer2012.ConnectionString);
            Processor = new SqlServerProcessor(new[] { "SqlServer2012" }, Connection, new SqlServer2012Generator(), new TextWriterAnnouncer(TestContext.Out), new ProcessorOptions(), new SqlServerDbFactory());
            Connection.Open();
            Processor.BeginTransaction();
        }

        [TearDown]
        public void TearDown()
        {
            Processor?.CommitTransaction();
            Processor?.Dispose();
        }

        [Test]
        public override void CallingSchemaExistsReturnsFalseIfSchemaDoesNotExist()
        {
            Processor.SchemaExists("DoesNotExist").ShouldBeFalse();
        }

        [Test]
        public override void CallingSchemaExistsReturnsTrueIfSchemaExists()
        {
            Processor.SchemaExists("dbo").ShouldBeTrue();
        }
    }
}
