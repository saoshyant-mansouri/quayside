using Quayside.Core.Sql;

namespace Quayside.Infrastructure.Sql;

public interface IEmbeddedCardSource
{
    Task<IReadOnlyList<EmbeddedTableCard>> LoadEmbeddedCardsAsync(CancellationToken cancellationToken);
}
