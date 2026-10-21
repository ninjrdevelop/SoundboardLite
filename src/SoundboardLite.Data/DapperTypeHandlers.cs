using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dapper;

namespace SoundboardLite.Data;

internal static class DapperTypeHandlers
{
#pragma warning disable CA2255 // intentional: registers the handler once, as soon as this assembly loads
    [ModuleInitializer]
    internal static void Register()
    {
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new GuidHandler());
    }
#pragma warning restore CA2255

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) =>
            DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>SQLite has no native GUID type; columns store the string form, so Dapper needs an explicit bridge.</summary>
    private sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value) => Guid.Parse((string)value);

        public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value.ToString();
    }
}
