using System.Text.Json.Serialization;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal sealed record GenericHttpBoardsResponse(
    [property: JsonPropertyName("boards")] IReadOnlyList<GenericHttpBoardDto> Boards);

internal sealed record GenericHttpCardsResponse(
    [property: JsonPropertyName("cards")] IReadOnlyList<GenericHttpCardDto> Cards);

internal sealed record GenericHttpBoardDto(
    [property: JsonPropertyName("board_id")] string BoardId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("board_title")] string? BoardTitle);

internal sealed record GenericHttpCardDto(
    [property: JsonPropertyName("card_id")] string CardId,
    [property: JsonPropertyName("board_id")] string BoardId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("column_id")] string? ColumnId,
    [property: JsonPropertyName("column_title")] string? ColumnTitle,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("archived")] bool Archived,
    [property: JsonPropertyName("card_version")] string? CardVersion,
    [property: JsonPropertyName("web_url")] string? WebUrl);
