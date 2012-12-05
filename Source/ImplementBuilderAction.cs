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

            var cls = classDeclaration.DeclaredElement as IClass;
            if (cls == null)
            {
                return;
            }

            var ctor = cls.Constructors.OrderByDescending(x => x.Parameters.Count).FirstOrDefault();
            if (ctor == null)
                return;

            foreach (var parameter in ctor.Parameters)
            {
                code.AppendLine("private {0} _{1};", parameter.Type.GetPresentableName(cls.PresentationLanguage), parameter.ShortName);
                code.AppendLine("public {2} With{1}({0} value){{ ", parameter.Type.GetPresentableName(cls.PresentationLanguage), parameter.ShortName.Capitalize(), builderType);
                code.AppendLine(" _{0} = value;", parameter.ShortName);
                code.AppendLine("return this;");
                code.AppendLine("}");
                code.AppendLine();
            }

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
