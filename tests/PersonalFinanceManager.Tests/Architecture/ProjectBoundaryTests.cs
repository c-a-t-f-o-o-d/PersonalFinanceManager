using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Tests.Architecture
{
    [TestClass]
    public sealed class ProjectBoundaryTests
    {
        [TestMethod]
        public void ApplicationAssembly_DoesNotReferenceDataOrApp()
        {
            string[] referencedNames = typeof(IAccountWorkflowService)
                .Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name ?? string.Empty)
                .ToArray();

            CollectionAssert.DoesNotContain(referencedNames, "PersonalFinanceManager.Data");
            CollectionAssert.DoesNotContain(referencedNames, "PersonalFinanceManager.App");
        }
    }
}
