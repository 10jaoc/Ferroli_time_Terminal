using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace FerroliTime.Terminal.Services;

public record Empleado(string MP_CODI, string? Descripcion);

public class MarcajeDia
{
    public string MP_HORA { get; set; } = "";
    public short ENTRADA { get; set; }
    public string? DESC_LOCA { get; set; }

    // ENTRADA = 0 es una entrada y 1 una salida (igual que terminal.asp y el backoffice).
    public string Tipo => ENTRADA == 0 ? "Entrada" : "Salida";
    public string HoraTexto => MP_HORA.Length == 6 ? $"{MP_HORA[..2]}:{MP_HORA[2..4]}:{MP_HORA[4..6]}" : MP_HORA;
}

/// <summary>Consultas de terminal.asp sobre FERROLI_TIMER (con parámetros, sin concatenar SQL).</summary>
public class TerminalRepository(string connStr)
{
    private SqlConnection Conexion() => new(connStr);

    /// <summary>IP de un PC común (parámetros activos 500 a 599): se ficha tecleando el nº de empleado.</summary>
    public async Task<bool> EsPcComunAsync(string ip)
    {
        using var conn = Conexion();
        return await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.CMSParametros
            WHERE IdParametro BETWEEN 500 AND 599 AND Status = 'ACT'
              AND LTRIM(RTRIM(ValorAlfanumerico)) = @ip", new { ip }) > 0;
    }

    /// <summary>
    /// Empleado activo cuyo terminal personal (telefonos_aut.Id_telefono) es esta IP, o null.
    /// Los desactivados no cuentan: su IP puede haberse reasignado a otro empleado.
    /// </summary>
    public async Task<string?> EmpleadoDeTerminalAsync(string ip)
    {
        using var conn = Conexion();
        return (await conn.ExecuteScalarAsync<string?>(@"
            SELECT TOP 1 MP_CODI FROM dbo.telefonos_aut
            WHERE LTRIM(RTRIM(Id_telefono)) = @ip AND Status = 'ACT' ORDER BY MP_CODI", new { ip }))?.Trim();
    }

    /// <summary>
    /// Empleado activo con acceso a la configuración del terminal (telefonos_aut.config = 1)
    /// cuyo terminal personal es esta IP, o null.
    /// </summary>
    public async Task<Empleado?> ConfiguradorDeTerminalAsync(string ip)
    {
        using var conn = Conexion();
        return await conn.QuerySingleOrDefaultAsync<Empleado>(@"
            SELECT TOP 1 RTRIM(MP_CODI) AS MP_CODI, descripcion AS Descripcion FROM dbo.telefonos_aut
            WHERE LTRIM(RTRIM(Id_telefono)) = @ip AND Status = 'ACT' AND config = 1
            ORDER BY MP_CODI", new { ip });
    }

    /// <summary>
    /// Oficina abierta: el parámetro 10 no tiene Valor numérico 0 y la hora está dentro del
    /// horario del parámetro 11 (Numérico desde / hasta, en horas decimales).
    /// </summary>
    public async Task<bool> OficinaAbiertaAsync(DateTime ahora)
    {
        var hora = ahora.Hour + ahora.Minute / 60.0 + ahora.Second / 3600.0;
        using var conn = Conexion();
        return await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.CMSParametros
            WHERE (IdParametro = 10 AND ValorNumerico = 0)
               OR (IdParametro = 11 AND (NumericoHasta < @hora OR NumericoDesde > @hora))", new { hora }) == 0;
    }

    /// <summary>
    /// Empleado activo. Desde un PC común solo valen los de reloj 99 (como en terminal.asp);
    /// desde su terminal personal, cualquiera.
    /// </summary>
    public async Task<Empleado?> EmpleadoAsync(string codigo, bool soloReloj99)
    {
        using var conn = Conexion();
        return await conn.QuerySingleOrDefaultAsync<Empleado>($@"
            SELECT TOP 1 RTRIM(MP_CODI) AS MP_CODI, descripcion AS Descripcion FROM dbo.telefonos_aut
            WHERE MP_CODI = @codigo AND Status = 'ACT'{(soloReloj99 ? " AND MP_RELO = '99'" : "")}",
            new { codigo });
    }

    public async Task<List<MarcajeDia>> MarcajesDelDiaAsync(string codigo, DateTime dia)
    {
        using var conn = Conexion();
        return (await conn.QueryAsync<MarcajeDia>(@"
            SELECT MP_HORA, ENTRADA, DESC_LOCA FROM dbo.Marcajes
            WHERE MP_CODI = @codigo AND MP_FECH = @fecha
            ORDER BY Id_registro DESC", new { codigo, fecha = Fecha(dia) })).ToList();
    }

    /// <summary>Sin marcajes hoy o con una salida como último: toca ENTRADA; si no, SALIDA.</summary>
    public static bool TocaEntrada(List<MarcajeDia> delDia) => delDia.Count == 0 || delDia[0].ENTRADA != 0;

    /// <summary>
    /// Graba el marcaje salvo que el empleado ya tenga otro en los últimos <paramref name="minutos"/>
    /// minutos (la comprobación va en el propio INSERT). Devuelve false si no se ha grabado.
    /// </summary>
    public async Task<bool> FicharAsync(string codigo, bool entrada, DateTime ahora, int minutos, string ip, string localizacion)
    {
        var desde = ahora.AddMinutes(-minutos);
        using var conn = Conexion();
        return await conn.ExecuteAsync(@"
            INSERT INTO dbo.Marcajes
                (MP_CODI, MP_FECH, MP_HORA, ENTRADA, Id_telefono, DESC_LOCA, Status, CrtUser, CrtDate, CrtProcess)
            SELECT @codigo, @fecha, @hora, @tipo, @ip, @localizacion, 'ACT', @ip, GETDATE(), 'FerroliTime.Terminal'
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.Marcajes
                WHERE MP_CODI = @codigo AND MP_FECH = @fecha AND MP_HORA BETWEEN @horaDesde AND @hora)",
            new
            {
                codigo,
                fecha = Fecha(ahora),
                hora = Hora(ahora),
                tipo = (short)(entrada ? 0 : 1),
                ip,
                localizacion,
                horaDesde = desde.Date == ahora.Date ? Hora(desde) : "000000",
            }) > 0;
    }

    // MP_FECH se guarda como AAAAMMDD y MP_HORA como HHMMSS.
    private static string Fecha(DateTime d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    private static string Hora(DateTime d) => d.ToString("HHmmss", CultureInfo.InvariantCulture);
}
