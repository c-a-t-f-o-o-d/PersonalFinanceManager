using System.Reflection;
using PersonalFinanceManager.Application.Services.Contracts;

namespace PersonalFinanceManager.Tests.Services
{
    [TestClass]
    public sealed class WorkflowServiceBoundaryTests
    {
        [TestMethod]
        public void WorkflowContracts_DoNotExposeRepositoryTypes()
        {
            Type[] contracts = typeof(IAccountWorkflowService).Assembly
                .GetTypes()
                .Where(type => type.IsInterface && type.Namespace == "PersonalFinanceManager.Application.Services.Contracts")
                .ToArray();

            foreach (Type contract in contracts)
            {
                foreach (MethodInfo method in contract.GetMethods())
                {
                    AssertNoRepositoryType(contract, method.ReturnType, $"{contract.Name}.{method.Name} return type");

                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        AssertNoRepositoryType(contract, parameter.ParameterType, $"{contract.Name}.{method.Name} parameter {parameter.Name}");
                    }
                }
            }
        }

        private static void AssertNoRepositoryType(Type contract, Type type, string context)
        {
            foreach (Type inspected in Flatten(type))
            {
                string? namespaceName = inspected.Namespace;
                Assert.IsFalse(namespaceName?.StartsWith("PersonalFinanceManager.Data.Repositories", StringComparison.Ordinal) == true, $"{context} exposes repository type {inspected.FullName} from {contract.FullName}.");
            }
        }

        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;

            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    foreach (Type nested in Flatten(argument))
                    {
                        yield return nested;
                    }
                }
            }

            if (type.HasElementType && type.GetElementType() is Type elementType)
            {
                foreach (Type nested in Flatten(elementType))
                {
                    yield return nested;
                }
            }
        }
    }
}
