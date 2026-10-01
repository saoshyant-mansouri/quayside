using Microsoft.Data.SqlTypes;
using Quayside.Infrastructure.Data;

namespace Quayside.Infrastructure.Sql;

public static class Vectors
{
    public static SqlVector<float> ToSql(ReadOnlyMemory<float> embedding)
    {
        if (embedding.Length != VectorSpec.Dimensions)
            throw new ArgumentException($"Embedding has {embedding.Length} dimensions; the database stores {VectorSpec.Dimensions}.", nameof(embedding));

        return new SqlVector<float>(embedding);
    }
}
