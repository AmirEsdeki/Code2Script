using Code2Script.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TableGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Assembly a = Assembly.LoadFile(@"C:\MyData\Desktop\Aban 400\TPG.Acc.Microservice\src\TPG.Acc.Microservice.Core\bin\Debug\net5.0\TPG.Acc.Microservice.Core.dll");

            List<Type> types = a.GetTypes().ToList();

            new TableScriptCreator(types).CreateScriptsIntoSeprateFiles();
        }
    }

    public class TableScriptCreator
    {
        private readonly List<TableClass> _tables = new();

        public TableScriptCreator(List<Type> types)
        {
            var sqlTypes = (from t in types
                            where t.IsClass && t.GetCustomAttribute(typeof(SqlTable)) is not null
                            select t).ToArray();

            foreach (Type t in sqlTypes)
            {
                TableClass tc = new TableClass(t);
                _tables.Add(tc);
            }
        }

        public void CreateScriptsIntoSeprateFiles(string path = null)
        {
            foreach (TableClass table in _tables)
            {
                string createScript = table.CreateTableScript();

                StringBuilder FkScript = new StringBuilder(createScript);

                foreach (Property field in table.Fields)
                {
                    var relatedTable = _tables.Where(t => field.Type.Name == t.ClassName).FirstOrDefault();

                    if (relatedTable is not null)
                    {
                        FkScript.AppendLine("GO");
                        FkScript.AppendLine("ALTER TABLE " + table.ClassName);
                        if (field.WithNoCheck) FkScript.Append(" WITH NOCHECK");
                        FkScript.AppendLine("ADD CONSTRAINT FK_" + field.Name + $" FOREIGN KEY ( {field.Name}Id ) REFERENCES " + relatedTable.ClassName + "(Id)");
                        FkScript.AppendLine("GO");
                    }
                }

                Console.WriteLine(createScript);
            }
        }
    }

    public class Property
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public int? Length { get; set; }
        public string MapToType { get; set; }
        public bool Required { get; set; }
        public bool NotRequired { get; set; }
        public bool Identity { get; set; }
        public bool PrimaryKey { get; set; }
        public bool NotPrimaryKey { get; set; }
        public bool WithNoCheck { get; set; }
        public bool Complex { get; set; }
    }

    public class TableClass
    {
        private List<Property> _fieldInfo = new List<Property>();
        private string _className = String.Empty;
        private string _schema = String.Empty;

        private Dictionary<Type, String> dataMapper
        {
            get
            {
                // Add the rest of your CLR Types to SQL Types mapping here
                Dictionary<Type, String> dataMapper = new Dictionary<Type, string>();
                dataMapper.Add(typeof(int), "INT");
                dataMapper.Add(typeof(int?), "INT");
                dataMapper.Add(typeof(long), "BIGINT");
                dataMapper.Add(typeof(long?), "BIGINT");
                dataMapper.Add(typeof(string), "NVARCHAR(MAX)");
                dataMapper.Add(typeof(bool), "BIT");
                dataMapper.Add(typeof(bool?), "BIT");
                dataMapper.Add(typeof(DateTime), "DATETIME2");
                dataMapper.Add(typeof(DateTime?), "DATETIME2");
                dataMapper.Add(typeof(float), "FLOAT");
                dataMapper.Add(typeof(float?), "FLOAT");
                dataMapper.Add(typeof(decimal), "DECIMAL(18,0)");
                dataMapper.Add(typeof(decimal?), "DECIMAL(18,0)");
                dataMapper.Add(typeof(Guid), "UNIQUEIDENTIFIER");
                dataMapper.Add(typeof(Guid?), "UNIQUEIDENTIFIER");

                return dataMapper;
            }
        }

        public List<Property> Fields
        {
            get { return this._fieldInfo; }
            set { this._fieldInfo = value; }
        }

        public string ClassName
        {
            get { return this._className; }
            set { this._className = value; }
        }

        public string Schema
        {
            get { return this._schema; }
            set { this._schema = value; }
        }

        public TableClass(Type t)
        {
            this._className = t.Name;
            var schema = (t.GetCustomAttribute(typeof(Schema), false) as Schema);
            this._schema = schema is not null ? schema.Value : "dbo";

            foreach (PropertyInfo p in t.GetProperties())
            {
                Property field = new Property();
                var length = (p.GetCustomAttribute(typeof(Length), false) as Length);
                field.Name = p.Name;
                field.Type = p.PropertyType;
                field.Length = length is not null && length.Value < 5000 ? length.Value : null;
                field.MapToType = (p.GetCustomAttribute(typeof(MapToType), false) as MapToType)?.Type;
                field.Required = p.GetCustomAttribute(typeof(Required), false) is not null;
                field.Identity = p.GetCustomAttribute(typeof(Identity), false) is not null;
                field.PrimaryKey = p.GetCustomAttribute(typeof(PrimaryKey), false) is not null;
                field.NotPrimaryKey = p.GetCustomAttribute(typeof(NotPrimaryKey), false) is not null;
                field.WithNoCheck = p.GetCustomAttribute(typeof(WithNoCheck), false) is not null;
                field.Complex = p.PropertyType.IsClass && p.PropertyType != typeof(string);
                this.Fields.Add(field);
            }
        }

        public string CreateTableScript()
        {
            StringBuilder script = new StringBuilder();
            var fields = this.Fields.Where(f => !f.Complex).ToList();

            script.AppendLine("CREATE TABLE " + this.Schema + "." + this.ClassName);
            script.AppendLine("(");

            for (int i = 0; i < fields.Count(); i++)
            {
                Property field = fields[i];

                if (field.MapToType is not null)
                {
                    script.Append("\t " + field.Name + $" {field.MapToType} ");
                }
                else if (dataMapper.ContainsKey(field.Type))
                {
                    if (field.Type == typeof(string) && field.Length is not null)
                    {
                        script.Append("\t " + field.Name + $" NVARCHAR({field.Length}) ");
                    }
                    else
                    {
                        script.Append("\t " + field.Name + $" {dataMapper[field.Type]} ");
                    }
                }
                else
                {
                    if (field.Type.IsEnum || IsNullableEnum(field.Type))
                    {
                        script.Append("\t " + field.Name + $" INT ");
                    }
                    else
                    {
                    // if complex type? 
                        continue;
                    }
                }

                if (field.Identity)
                {
                    script.Append("PRIMARY KEY IDENTITY(1,1) ");
                }

                if (!IsNullable(field.Type) || field.Required)
                {
                    script.Append("NOT ");
                }

                script.Append("NULL");

                if (i != fields.Count() - 1)
                {
                    script.Append(",");
                }

                script.Append(Environment.NewLine);
            }

            script.AppendLine(")" + "\t" + "ON [PRIMARY]");
            script.AppendLine("GO");
            script.AppendLine(Environment.NewLine);

            if (!Fields.Any(f => f.Identity))
            {
                if (Fields.Any(f => f.Name.ToLower() == "id" && !f.NotPrimaryKey))
                {
                    script.AppendLine(CreatePrimaryKey(Fields.Where(f => f.Name.ToLower() == "id"
                    && !f.NotPrimaryKey).FirstOrDefault().Name));
                }
                else
                {
                    var primaryKey = Fields.Where(f => f.PrimaryKey);

                    if (primaryKey.Count() > 1)
                    {
                        throw new Exception($"Only one primary key could exist in the {ClassName} model!");
                    }

                    script.AppendLine(CreatePrimaryKey(primaryKey.FirstOrDefault().Name));
                }
            }

            return script.ToString();
        }

        public bool IsNullable(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        public bool IsNullableEnum(Type t)
        {
            Type u = Nullable.GetUnderlyingType(t);
            return (u != null) && u.IsEnum;
        }

        public string CreatePrimaryKey(string column)
        {
            var script = new StringBuilder();
            script.AppendLine("ALTER TABLE " + this.Schema + "." + this.ClassName);
            script.AppendLine("ADD CONSTRAINT PK_" + this.ClassName);
            script.AppendLine("PRIMARY KEY CLUSTERED ("
                + column + ") ON [PRIMARY];");
            script.AppendLine("GO");
            return script.ToString();
        }
    }
}