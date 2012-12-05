using System;
using System.Linq;
using System.Text;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.Bulbs;
using JetBrains.ReSharper.Intentions.Extensibility;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace GenerateBuilder.Source
{
    [ContextAction(Group = "C#", Name = "Implement Builder class",Description = "Inserts a builder for this class.")]
    public class ImplementBuilderContextAction : CSharpOneItemContextAction
    {
        private IClassDeclaration _theclass;


        public ImplementBuilderContextAction(ICSharpContextActionDataProvider provider)
            : base(provider)
        {
        }

        protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
        {
            var factory = CSharpElementFactory.GetInstance(_theclass.GetPsiModule());
            if (factory == null)
            {
                return null;
            }

            AddProxyClass(_theclass, factory);
            return null;
        }

        private static void AddProxyClass(IClassDeclaration classDeclaration, CSharpElementFactory factory)
        {
            var builderType = string.Format("{0}Builder", classDeclaration.DeclaredName);

            var code = new StringBuilder(string.Format("public class {0} {{", builderType));
            code.AppendLine();
            var cls = classDeclaration.DeclaredElement as IClass;
            if (cls == null)
            {
                return;
            }

            var ctor = cls.Constructors.OrderByDescending(x => x.Parameters.Count).FirstOrDefault();
            if (ctor == null)
                return;

            var fields = new StringBuilder();
            var methods = new StringBuilder();

            foreach (var parameter in ctor.Parameters)
            {
                var typePresentableName = parameter.Type.GetPresentableName(cls.PresentationLanguage);
                var shortName = parameter.ShortName;
                var capitalizedShortName = shortName.Capitalize();

                fields.AppendLine("private {0} _{1};", typePresentableName, shortName);
                methods.AppendLine("public {2} With{1}({0} value){{ ", typePresentableName, capitalizedShortName, builderType);
                methods.AppendLine(" _{0} = value;", shortName);
                methods.AppendLine("return this;");
                methods.AppendLine("}");
                methods.AppendLine();

                if (parameter.Type.IsGenericIEnumerable())
                {
                    var genericParameter = typePresentableName.Split(new[] { '<', '>' })[1];
                    var listType = string.Format("List<{0}>", genericParameter);

                    methods.AppendLine("public {0} Add{1}({2} value){{", builderType, NounUtil.GetSingular(capitalizedShortName), genericParameter);
                    methods.AppendLine(" if(_{0} == null){{", shortName);
                    methods.AppendLine("  _{0} = new {1}();", shortName, listType);
                    methods.AppendLine(" }");
                    methods.AppendLine(" (({0})_{1}).Add(value);", listType, shortName);
                    methods.AppendLine(" return this;");
                    methods.AppendLine("}");
                    methods.AppendLine();
                }

            }

            code.Append(fields);
            code.Append(methods);


            code.AppendLine("public {0} Build(){{", classDeclaration.DeclaredName);
            code.AppendFormat("return new  {0}(", classDeclaration.DeclaredName);
            code.Append(ctor.Parameters.Select(x => string.Format("_{0}", x.ShortName)).ToArray().Join(", "));
            code.AppendLine(");");
            code.AppendLine("}");


            code.Append("}");

            var memberDeclaration = factory.CreateTypeMemberDeclaration(code.ToString()) as IClassDeclaration;

            var namespaceDeclaration = classDeclaration.GetContainingNamespaceDeclaration();

            namespaceDeclaration.AddTypeDeclarationAfter(memberDeclaration, classDeclaration);
        }

        public override string Text
        {
            get { return "Implement builder"; }
        }

        public override bool IsAvailable(IUserDataHolder cache)
        {
            var currentclass = Provider.GetSelectedElement<IClassDeclaration>(true, true);
            if (currentclass != null && !currentclass.IsAbstract)
            {
                _theclass = currentclass;
                return true;
            }
            return false;
        }
    }


    public abstract class CSharpOneItemContextAction : BulbItemImpl, IContextAction
    {
        protected readonly ICSharpContextActionDataProvider Provider;

        protected CSharpOneItemContextAction(ICSharpContextActionDataProvider provider)
        {
            Provider = provider;
        }

        public abstract bool IsAvailable(IUserDataHolder cache);

        public new IBulbItem[] Items
        {
            get { return new[] { this }; }
        }
    }
}
