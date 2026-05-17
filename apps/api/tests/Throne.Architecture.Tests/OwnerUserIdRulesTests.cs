using System.Reflection;
using FluentAssertions;
using MongoDB.Bson.Serialization.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Throne.Architecture.Tests;

/// <summary>
/// Архитектурные правила multi-user изоляции (ADR-0012).
///
/// 1. user-owned Domain-агрегаты обязаны принимать <c>ownerUserId</c> в Create/Restore.
/// 2. Mongo-документы соответствующих коллекций обязаны иметь свойство <c>OwnerUserId</c>
///    с <c>[BsonElement("owner_user_id")]</c>.
/// 3. Mongo-репозитории user-owned коллекций обязаны зависеть от
///    <c>ICurrentUserAccessor</c> и фильтровать выборки по <c>owner_user_id</c>.
/// 4. Application-handler'ы, создающие user-owned агрегаты, обязаны зависеть от
///    <c>ICurrentUserAccessor</c>.
/// </summary>
public class OwnerUserIdRulesTests
{
    private static readonly Type[] UserOwnedAggregates =
    [
        typeof(Throne.Domain.Intents.IntentFactory),
        typeof(Throne.Domain.Auth.PersonalAccessToken),
        typeof(Throne.Domain.Dreams.DreamSession),
    ];

    private static readonly string[] UserOwnedDocumentTypeNames =
    [
        "Throne.Infrastructure.Mongo.Documents.IntentDocument",
        "Throne.Infrastructure.Mongo.Documents.IntentAttachmentDocument",
        "Throne.Infrastructure.Mongo.Documents.PersonalAccessTokenDocument",
        "Throne.Infrastructure.Mongo.Documents.InstructionPatchDocument",
        "Throne.Infrastructure.Mongo.Documents.DreamSessionDocument",
    ];

    private static readonly string[] UserOwnedRepositoryTypeNames =
    [
        "Throne.Infrastructure.Mongo.MongoIntentRepository",
        "Throne.Infrastructure.Mongo.MongoIntentAttachmentRepository",
        "Throne.Infrastructure.Mongo.MongoPersonalAccessTokenRepository",
        "Throne.Infrastructure.Mongo.MongoInstructionPatchRepository",
        "Throne.Infrastructure.Mongo.MongoDreamSessionRepository",
    ];

    // intent:aa5b39b430c54685a08f475d3be167b9: MongoIntentRepository is a slim
    // facade that delegates owner-scoped reads/writes to feature-area impls in
    // Throne.Infrastructure.Mongo.Intents.*. The IL-string scan must follow
    // into those delegates, otherwise the test only sees newobj forwarders.
    private static readonly Dictionary<string, string[]> RepositoryDelegateTypeNames = new()
    {
        ["Throne.Infrastructure.Mongo.MongoIntentRepository"] =
        [
            "Throne.Infrastructure.Mongo.Intents.MongoIntentReader",
            "Throne.Infrastructure.Mongo.Intents.MongoIntentLifecycle",
            "Throne.Infrastructure.Mongo.Intents.MongoIntentTextEditor",
            "Throne.Infrastructure.Mongo.Intents.MongoIntentStatusMutator",
            "Throne.Infrastructure.Mongo.Intents.MongoIntentOrderingMutator",
            "Throne.Infrastructure.Mongo.Intents.IntentCollectionFilters",
        ],
    };

    // Handlers, ВЫЗЫВАЮЩИЕ Domain.Create на user-owned агрегате. UploadIntentAttachmentHandler
    // не входит: IntentAttachment не имеет Domain-фабрики, его OwnerUserId проставляется
    // репозиторием (см. UserOwnedRepositories_depend_on_ICurrentUserAccessor).
    private static readonly Type[] HandlersThatCreateUserOwnedAggregates =
    [
        typeof(Throne.Application.Intents.CreateIntentHandler),
        typeof(Throne.Application.Auth.GenerateMcpTokenHandler),
        typeof(Throne.Application.InstructionPatches.ProposeInstructionPatchHandler),
        typeof(Throne.Application.Dreams.RecordDreamSessionHandler),
    ];

    [Fact(DisplayName = "User-owned Domain-агрегаты требуют ownerUserId в Create/Restore")]
    public void UserOwnedAggregates_require_ownerUserId_in_factories()
    {
        foreach (var type in UserOwnedAggregates)
        {
            var factories = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "Create" or "Restore")
                .ToList();
            factories.Should().NotBeEmpty($"{type.FullName} should expose Create/Restore");

            foreach (var factory in factories)
            {
                factory.GetParameters().Should().Contain(
                    p => p.Name == "ownerUserId" && p.ParameterType == typeof(string),
                    $"{type.FullName}.{factory.Name} обязан принимать ownerUserId:string первым/явным параметром " +
                    "(ADR-0012: multi-user изоляция). Без него агрегат может оказаться без владельца.");
            }
        }
    }

    [Fact(DisplayName = "Mongo-документы user-owned коллекций имеют [BsonElement(\"owner_user_id\")]")]
    public void UserOwnedDocuments_have_owner_user_id_element()
    {
        var assembly = typeof(Throne.Infrastructure.AssemblyMarker).Assembly;
        foreach (var name in UserOwnedDocumentTypeNames)
        {
            var type = assembly.GetType(name);
            type.Should().NotBeNull($"{name} must exist");

            var prop = type!.GetProperty(
                "OwnerUserId",
                BindingFlags.Public | BindingFlags.Instance);
            prop.Should().NotBeNull($"{name}.OwnerUserId must exist");
            prop!.PropertyType.Should().Be<string>();

            var attr = prop.GetCustomAttribute<BsonElementAttribute>();
            attr.Should().NotBeNull($"{name}.OwnerUserId must carry [BsonElement]");
            attr!.ElementName.Should().Be("owner_user_id");
        }
    }

    [Fact(DisplayName = "Mongo-репозитории user-owned коллекций зависят от ICurrentUserAccessor")]
    public void UserOwnedRepositories_depend_on_ICurrentUserAccessor()
    {
        var assembly = typeof(Throne.Infrastructure.AssemblyMarker).Assembly;
        foreach (var name in UserOwnedRepositoryTypeNames)
        {
            var type = assembly.GetType(name);
            type.Should().NotBeNull($"{name} must exist");

            var ctors = type!.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            ctors.Should().NotBeEmpty();
            ctors.Any(c => c.GetParameters().Any(p =>
                p.ParameterType == typeof(Throne.Application.Auth.ICurrentUserAccessor)))
                .Should().BeTrue(
                    $"{name} обязан инжектить ICurrentUserAccessor — иначе репо не может фильтровать " +
                    "выборки по owner_user_id и потенциально вернёт чужие данные. " +
                    "Добавь параметр ICurrentUserAccessor в конструктор + Filter.Eq(\"owner_user_id\", _user.UserId).");
        }
    }

    [Fact(DisplayName = "Mongo-репозитории user-owned коллекций ссылаются на owner_user_id")]
    public void UserOwnedRepositories_reference_owner_user_id_in_il()
    {
        var assemblyPath = typeof(Throne.Infrastructure.AssemblyMarker).Assembly.Location;
        using var module = ModuleDefinition.ReadModule(assemblyPath);

        foreach (var name in UserOwnedRepositoryTypeNames)
        {
            var type = module.GetType(name);
            type.Should().NotBeNull($"{name} must exist in IL");

            var typesToScan = new List<TypeDefinition> { type! };
            if (RepositoryDelegateTypeNames.TryGetValue(name, out var delegateNames))
            {
                foreach (var delegateName in delegateNames)
                {
                    var delegateType = module.GetType(delegateName);
                    delegateType.Should().NotBeNull($"{delegateName} (delegate of {name}) must exist in IL");
                    typesToScan.Add(delegateType!);
                }
            }

            var allInstructions = typesToScan
                .SelectMany(AllMethodsAndFields)
                .Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .ToList();

            var hits = allInstructions
                    .Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "owner_user_id")
                || allInstructions
                    .Any(i => i.Operand is FieldReference f && f.Name == "OwnerUserId")
                || allInstructions
                    .Any(i => i.Operand is MethodReference mr && mr.Name.Contains("OwnerUserId", StringComparison.Ordinal));

            hits.Should().BeTrue(
                $"{name}: в IL не нашли ни строки \"owner_user_id\", ни поля OwnerUserId, ни вызова *OwnerUserId*. " +
                "Без них фильтр по владельцу не применяется — это leakage между пользователями. " +
                "Добавь Filter.Eq(\"owner_user_id\", _user.UserId) в Find/Update/Delete.");
        }
    }

    [Fact(DisplayName = "Application-handler'ы создания user-owned зависят от ICurrentUserAccessor")]
    public void Handlers_creating_user_owned_aggregates_depend_on_ICurrentUserAccessor()
    {
        foreach (var type in HandlersThatCreateUserOwnedAggregates)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            ctors.Should().NotBeEmpty();
            ctors.Any(c => c.GetParameters().Any(p =>
                p.ParameterType == typeof(Throne.Application.Auth.ICurrentUserAccessor)))
                .Should().BeTrue(
                    $"{type.FullName} создаёт user-owned агрегат, поэтому обязан инжектить ICurrentUserAccessor " +
                    "и передавать _user.UserId в Domain.Create(...). Иначе создаваемый объект может оказаться без владельца.");
        }
    }

    private static IEnumerable<MethodDefinition> AllMethodsAndFields(TypeDefinition type)
    {
        foreach (var method in type.Methods)
        {
            yield return method;
        }
        foreach (var nested in type.NestedTypes)
        {
            foreach (var method in AllMethodsAndFields(nested))
            {
                yield return method;
            }
        }
    }
}
