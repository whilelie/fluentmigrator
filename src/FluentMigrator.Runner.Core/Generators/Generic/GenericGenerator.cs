using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FluentMigrator.Expressions;
using FluentMigrator.Model;
using FluentMigrator.Runner.Generators.Base;

namespace FluentMigrator.Runner.Generators.Generic
{
    public abstract class GenericGenerator : GeneratorBase
    {
        public CompatabilityMode compatabilityMode;

        public GenericGenerator(IColumn column, IQuoter quoter, IDescriptionGenerator descriptionGenerator)
            : base(column, quoter, descriptionGenerator)
        {
            compatabilityMode = CompatabilityMode.LOOSE;
        }

        public virtual string CreateTable { get { return "CREATE TABLE {0} ({1})"; } }
        public virtual string DropTable { get { return "DROP TABLE {0}"; } }

        public virtual string AddColumn { get { return "ALTER TABLE {0} ADD COLUMN {1}"; } }
        public virtual string DropColumn { get { return "ALTER TABLE {0} DROP COLUMN {1}"; } }
        public virtual string AlterColumn { get { return "ALTER TABLE {0} ALTER COLUMN {1}"; } }
        public virtual string RenameColumn { get { return "ALTER TABLE {0} RENAME COLUMN {1} TO {2}"; } }

        public virtual string RenameTable { get { return "RENAME TABLE {0} TO {1}"; } }

        public virtual string CreateSchema { get { return "CREATE SCHEMA {0}"; } }
        public virtual string AlterSchema { get { return "ALTER SCHEMA {0} TRANSFER {1}"; } }
        public virtual string DropSchema { get { return "DROP SCHEMA {0}"; } }

        public virtual string CreateIndex { get { return "CREATE {0}{1}INDEX {2} ON {3} ({4})"; } }
        public virtual string DropIndex { get { return "DROP INDEX {0}"; } }

        public virtual string InsertData { get { return "INSERT INTO {0} ({1}) VALUES ({2})"; } }
        public virtual string UpdateData { get { return "UPDATE {0} SET {1} WHERE {2}"; } }
        public virtual string DeleteData { get { return "DELETE FROM {0} WHERE {1}"; } }

        public virtual string CreateConstraint { get { return "ALTER TABLE {0} ADD CONSTRAINT {1} {2} ({3})"; } }
        public virtual string DeleteConstraint { get { return "ALTER TABLE {0} DROP CONSTRAINT {1}"; } }
        public virtual string CreateForeignKeyConstraint { get { return "ALTER TABLE {0} ADD {1}"; } }

        public virtual string GetUniqueString(CreateIndexExpression column)
        {
            return column.Index.IsUnique ? "UNIQUE " : string.Empty;
        }

        public virtual string GetClusterTypeString(CreateIndexExpression column)
        {
            return string.Empty;
        }

        /// <summary>
        /// Outputs a create table string
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override string Generate(CreateTableExpression expression)
        {
            if (string.IsNullOrEmpty(expression.TableName)) throw new ArgumentNullException("expression", "expression.TableName cannot be empty");
            if (expression.Columns.Count == 0) throw new ArgumentException("You must specifiy at least one column");

            string quotedTableName = Quoter.QuoteTableName(expression.TableName, expression.SchemaName);

            string errors = ValidateAdditionalFeatureCompatibility(expression.Columns.SelectMany(x => x.AdditionalFeatures));
            if (!string.IsNullOrEmpty(errors)) return errors;

            return string.Format(CreateTable, quotedTableName, Column.Generate(expression.Columns, quotedTableName));
        }

        public override string Generate(DeleteTableExpression expression)
        {
            return String.Format(DropTable, Quoter.QuoteTableName(expression.TableName, expression.SchemaName));
        }

        public override string Generate(RenameTableExpression expression)
        {
            return String.Format(RenameTable, Quoter.QuoteTableName(expression.OldName, expression.SchemaName), Quoter.Quote(expression.NewName));
        }

        public override string Generate(CreateColumnExpression expression)
        {
            string errors = ValidateAdditionalFeatureCompatibility(expression.Column.AdditionalFeatures);
            if (!string.IsNullOrEmpty(errors)) return errors;

            return String.Format(AddColumn, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), Column.Generate(expression.Column));
        }


        public override string Generate(AlterColumnExpression expression)
        {
            string errors = ValidateAdditionalFeatureCompatibility(expression.Column.AdditionalFeatures);
            if (!string.IsNullOrEmpty(errors)) return errors;

            return String.Format(AlterColumn, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), Column.Generate(expression.Column));
        }

        public override string Generate(DeleteColumnExpression expression)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string columnName in expression.ColumnNames)
            {
                if (expression.ColumnNames.First() != columnName)
                {
                    AppendSqlStatementEndToken(builder);
                }

                builder.AppendFormat(DropColumn, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), Quoter.QuoteColumnName(columnName));
            }
            return builder.ToString();
        }

        public override string Generate(RenameColumnExpression expression)
        {
            return String.Format(RenameColumn,
                Quoter.QuoteTableName(expression.TableName, expression.SchemaName),
                Quoter.QuoteColumnName(expression.OldName),
                Quoter.QuoteColumnName(expression.NewName)
                );
        }

        public override string Generate(CreateIndexExpression expression)
        {

            string[] indexColumns = new string[expression.Index.Columns.Count];
            IndexColumnDefinition columnDef;


            for (int i = 0; i < expression.Index.Columns.Count; i++)
            {
                columnDef = expression.Index.Columns.ElementAt(i);
                if (columnDef.Direction == Direction.Ascending)
                {
                    indexColumns[i] = Quoter.QuoteColumnName(columnDef.Name) + " ASC";
                }
                else
                {
                    indexColumns[i] = Quoter.QuoteColumnName(columnDef.Name) + " DESC";
                }
            }

            return String.Format(CreateIndex
                , GetUniqueString(expression)
                , GetClusterTypeString(expression)
                , Quoter.QuoteIndexName(expression.Index.Name)
                , Quoter.QuoteTableName(expression.Index.TableName, expression.Index.SchemaName)
                , String.Join(", ", indexColumns));
        }

        public override string Generate(DeleteIndexExpression expression)
        {
            return String.Format(DropIndex, Quoter.QuoteIndexName(expression.Index.Name), Quoter.QuoteTableName(expression.Index.TableName, expression.Index.SchemaName));
        }

        public override string Generate(CreateForeignKeyExpression expression)
        {
            return string.Format(
                CreateForeignKeyConstraint,
                Quoter.QuoteTableName(expression.ForeignKey.ForeignTable, expression.ForeignKey.ForeignTableSchema),
                Column.FormatForeignKey(expression.ForeignKey, GenerateForeignKeyName));
        }

        public override string Generate(CreateConstraintExpression expression)
        {

            var constraintType = (expression.Constraint.IsPrimaryKeyConstraint) ? "PRIMARY KEY" : "UNIQUE";

            string[] columns = new string[expression.Constraint.Columns.Count];

            for (int i = 0; i < expression.Constraint.Columns.Count; i++)
            {
                columns[i] = Quoter.QuoteColumnName(expression.Constraint.Columns.ElementAt(i));
            }

            return string.Format(CreateConstraint, Quoter.QuoteTableName(expression.Constraint.TableName, expression.Constraint.SchemaName),
                Quoter.QuoteConstraintName(expression.Constraint.ConstraintName),
                constraintType,
                String.Join(", ", columns));
        }

        public override string Generate(DeleteConstraintExpression expression)
        {
            return string.Format(DeleteConstraint, Quoter.QuoteTableName(expression.Constraint.TableName, expression.Constraint.SchemaName), Quoter.QuoteConstraintName(expression.Constraint.ConstraintName));
        }

        public virtual string GenerateForeignKeyName(ForeignKeyDefinition foreignKey)
        {
            return Column.GenerateForeignKeyName(foreignKey);
        }

        public override string Generate(DeleteForeignKeyExpression expression)
        {
            if (expression.ForeignKey.ForeignTable == null)
                throw new ArgumentNullException("Table name not specified, ensure you have appended the OnTable extension. Format should be Delete.ForeignKey(KeyName).OnTable(TableName)");

            return string.Format(DeleteConstraint, Quoter.QuoteTableName(expression.ForeignKey.ForeignTable, expression.ForeignKey.ForeignTableSchema), Quoter.QuoteColumnName(expression.ForeignKey.Name));
        }

        public override string Generate(InsertDataExpression expression)
        {
            string errors = ValidateAdditionalFeatureCompatibility(expression.AdditionalFeatures);
            if (!string.IsNullOrEmpty(errors)) return errors;

            var output = new StringBuilder();
            foreach (var pair in GenerateColumnNamesAndValues(expression))
            {
                if (output.Length != 0)
                {
                    AppendSqlStatementEndToken(output);
                }

                output.AppendFormat(
                    InsertData,
                    Quoter.QuoteTableName(expression.TableName, expression.SchemaName),
                    pair.Key,
                    pair.Value);
            }

            return output.ToString();
        }

        protected virtual StringBuilder AppendSqlStatementEndToken(StringBuilder stringBuilder)
        {
            return stringBuilder.Append("; ");
        }

        protected List<KeyValuePair<string,string>> GenerateColumnNamesAndValues(InsertDataExpression expression)
        {
            var insertStrings = new List<KeyValuePair<string, string>>();

            foreach (InsertionDataDefinition row in expression.Rows)
            {
                var columnNames = new List<string>();
                var columnValues = new List<string>();
                foreach (KeyValuePair<string, object> item in row)
                {
                    columnNames.Add(Quoter.QuoteColumnName(item.Key));
                    columnValues.Add(Quoter.QuoteValue(item.Value));
                }

                var columns = String.Join(", ", columnNames.ToArray());
                var values = String.Join(", ", columnValues.ToArray());
                insertStrings.Add(new KeyValuePair<string, string>(columns, values));
            }

            return insertStrings;
        }

        protected string ValidateAdditionalFeatureCompatibility(IEnumerable<KeyValuePair<string, object>> features)
        {
            if (compatabilityMode == CompatabilityMode.STRICT) {
                List<string> unsupportedFeatures =
                    features.Where(x => !IsAdditionalFeatureSupported(x.Key)).Select(x => x.Key).ToList();

                if (unsupportedFeatures.Any()) {
                    string errorMessage =
                        string.Format(
                            "The following database specific additional features are not supported in strict mode [{0}]",
                            unsupportedFeatures.Aggregate((x, y) => x + ", " + y));
                    {
                        return compatabilityMode.HandleCompatabilty(errorMessage);
                    }
                }
            }
            return string.Empty;
        }

        public override string Generate(UpdateDataExpression expression)
        {

            List<string> updateItems = new List<string>();
            List<string> whereClauses = new List<string>();

            foreach (var item in expression.Set)
            {
                updateItems.Add(string.Format("{0} = {1}", Quoter.QuoteColumnName(item.Key), Quoter.QuoteValue(item.Value)));
            }

            if(expression.IsAllRows)
            {
                whereClauses.Add("1 = 1");
            }
            else
            {
                foreach (var item in expression.Where)
                {
                    var op = item.Value == null || item.Value == DBNull.Value ? "IS" : "=";
                    whereClauses.Add(string.Format("{0} {1} {2}", Quoter.QuoteColumnName(item.Key),
                                                   op, Quoter.QuoteValue(item.Value)));
                }
            }
            return String.Format(UpdateData, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), String.Join(", ", updateItems.ToArray()), String.Join(" AND ", whereClauses.ToArray()));
        }

        public override string Generate(DeleteDataExpression expression)
        {
            var deleteItems = new List<string>();


            if (expression.IsAllRows)
            {
                deleteItems.Add(string.Format(DeleteData, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), "1 = 1"));
            }
            else
            {
                foreach (var row in expression.Rows)
                {
                    var whereClauses = new List<string>();
                    foreach (KeyValuePair<string, object> item in row)
                    {
                        var op = item.Value == null || item.Value == DBNull.Value ? "IS" : "=";
                        whereClauses.Add(
                            string.Format(
                                "{0} {1} {2}",
                                Quoter.QuoteColumnName(item.Key),
                                op,
                                Quoter.QuoteValue(item.Value)));
                    }

                    deleteItems.Add(string.Format(DeleteData, Quoter.QuoteTableName(expression.TableName, expression.SchemaName), String.Join(" AND ", whereClauses.ToArray())));
                }
            }

            var output = new StringBuilder();
            foreach (var deleteItem in deleteItems)
            {
                if (output.Length != 0)
                {
                    AppendSqlStatementEndToken(output);
                }

                output.Append(deleteItem);
            }

            return output.ToString();
        }


        //All Schema method throw by default as only Sql server 2005 and up supports them.
        public override string Generate(CreateSchemaExpression expression)
        {
            return compatabilityMode.HandleCompatabilty("Schemas are not supported");

        }

        public override string Generate(DeleteSchemaExpression expression)
        {
            return compatabilityMode.HandleCompatabilty("Schemas are not supported");
        }

        public override string Generate(AlterSchemaExpression expression)
        {
            return compatabilityMode.HandleCompatabilty("Schemas are not supported");
        }

        public override string Generate(CreateSequenceExpression expression)
        {
            var result = new StringBuilder(string.Format("CREATE SEQUENCE "));
            var seq = expression.Sequence;
            result.AppendFormat(Quoter.QuoteSequenceName(seq.Name, seq.SchemaName));

            if (seq.Increment.HasValue)
            {
                result.AppendFormat(" INCREMENT {0}", seq.Increment);
            }

            if (seq.MinValue.HasValue)
            {
                result.AppendFormat(" MINVALUE {0}", seq.MinValue);
            }

            if (seq.MaxValue.HasValue)
            {
                result.AppendFormat(" MAXVALUE {0}", seq.MaxValue);
            }

            if (seq.StartWith.HasValue)
            {
                result.AppendFormat(" START WITH {0}", seq.StartWith);
            }

            if (seq.Cache.HasValue)
            {
                result.AppendFormat(" CACHE {0}", seq.Cache);
            }

            if (seq.Cycle)
            {
                result.Append(" CYCLE");
            }

            return result.ToString();
        }

        public override string Generate(DeleteSequenceExpression expression)
        {
            var result = new StringBuilder(string.Format("DROP SEQUENCE "));
            result.AppendFormat(Quoter.QuoteSequenceName(expression.SequenceName, expression.SchemaName));
            return result.ToString();
        }
    }
}
