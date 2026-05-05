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
        typeof(Throne.Domain.Intents.Intent),
        typeof(Throne.Domain.Intents.Training.IntentQa),
        typeof(Throne.Domain.Intents.Training.IntentReview),
        typeof(Throne.Domain.DreamRuns.DreamRun),
    ];

    private static readonly string[] UserOwnedDocumentTypeNames =
    [
        "Throne.Infrastructure.Mongo.Documents.IntentDocument",
        "Throne.Infrastructure.Mongo.Documents.IntentQaDocument",
        "Throne.Infrastructure.Mongo.Documents.IntentReviewDocument",
        "Throne.Infrastructure.Mongo.Documents.IntentAttachmentDocument",
        "Throne.Infrastructure.Mongo.Documents.DreamRunDocument",
    ];

    private static readonly string[] UserOwnedRepositoryTypeNames =
    [
        "Throne.Infrastructure.Mongo.MongoIntentRepository",
        "Throne.Infrastructure.Mongo.MongoIntentTrainingRepository",
        "Throne.Infrastructure.Mongo.MongoIntentAttachmentRepository",
        "Throne.Infrastructure.Mongo.MongoDreamRunRepository",
    ];

    // Handlers, ВЫЗЫВАЮЩИЕ Domain.Create на user-owned агрегате. UploadIntentAttachmentHandler
    // не входит: IntentAttachment не имеет Domain-фабрики, его OwnerUserId проставляется
    // репозиторием (см. UserOwnedRepositories_depend_on_ICurrentUserAccessor).
    private static readonly Type[] HandlersThatCreateUserOwnedAggregates =
    [
        typeof(Throne.Application.Intents.CreateIntentHandler),
        typeof(Throne.Application.Intents.AddIntentQaHandler),
        typeof(Throne.Application.Intents.AddIntentReviewHandler),
        typeof(Throne.Application.DreamRuns.RunDreamHandler),
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
                    $"{type.FullName}.{factory.Name} must accept ownerUserId:string");
            }
        }
    }

    [Fact(DisplayName = "Mongo-документы user-owned коллекций имеют [BsonElement(\"owner_user_id\")]")]
    public void UserOwnedDocuments_have_owner_user_id_element()
    {
        var assembly = typeof(Throne.Infrastructure.DependencyInjection).Assembly;
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
        var assembly = typeof(Throne.Infrastructure.DependencyInjection).Assembly;
        foreach (var name in UserOwnedRepositoryTypeNames)
        {
            var type = assembly.GetType(name);
            type.Should().NotBeNull($"{name} must exist");

            var ctors = type!.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            ctors.Should().NotBeEmpty();
            ctors.Any(c => c.GetParameters().Any(p =>
                p.ParameterType == typeof(Throne.Application.Auth.ICurrentUserAccessor)))
                .Should().BeTrue($"{name} must inject ICurrentUserAccessor");
        }
    }

    [Fact(DisplayName = "Mongo-репозитории user-owned коллекций ссылаются на owner_user_id")]
    public void UserOwnedRepositories_reference_owner_user_id_in_il()
    {
        var assemblyPath = typeof(Throne.Infrastructure.DependencyInjection).Assembly.Location;
        using var module = ModuleDefinition.ReadModule(assemblyPath);

        foreach (var name in UserOwnedRepositoryTypeNames)
        {
            var type = module.GetType(name);
            type.Should().NotBeNull($"{name} must exist in IL");

            var hits = AllMethodsAndFields(type!)
                .SelectMany(m => m.HasBody ? m.Body.Instructions : Enumerable.Empty<Instruction>())
                .Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "owner_user_id")
                || AllMethodsAndFields(type!)
                    .Where(m => m.HasBody)
                    .SelectMany(m => m.Body.Instructions)
                    .Any(i => i.Operand is FieldReference f && f.Name == "OwnerUserId")
                || AllMethodsAndFields(type!)
                    .Where(m => m.HasBody)
                    .SelectMany(m => m.Body.Instructions)
                    .Any(i => i.Operand is MethodReference mr && mr.Name.Contains("OwnerUserId", StringComparison.Ordinal));

            hits.Should().BeTrue(
                $"{name} must reference OwnerUserId / \"owner_user_id\" — иначе фильтрация не работает.");
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
                .Should().BeTrue($"{type.FullName} must inject ICurrentUserAccessor");
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
