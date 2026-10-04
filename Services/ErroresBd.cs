using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace FerroliTime.Terminal.Services;

/// <summary>
/// Reconoce los errores de SqlClient que indican que no se ha podido conectar con SQL Server
/// (servidor caído o inaccesible, red, instancia, base de datos o usuario de la cadena de conexión),
/// para mostrar un aviso claro en lugar de la página de error genérica.
/// </summary>
public static class ErroresBd
{
    public const string MensajeSinConexion =
        "No se puede conectar con la base de datos. Inténtelo de nuevo en unos minutos o avise al administrador.";

    // Números de error de conexión: red/TCP (2, 40, 53, 64, 121, 233, 258, 1231, 10053, 10054,
    // 10060, 10061, 11001), instancia no encontrada (26), base de datos (4060) e inicio de sesión (18456).
    private static readonly HashSet<int> NumerosSinConexion =
        [2, 26, 40, 53, 64, 121, 233, 258, 1231, 4060, 10053, 10054, 10060, 10061, 11001, 18456];

    public static bool EsSinConexion(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is SqlException sql
                && (sql.InnerException is Win32Exception || sql.Errors.Cast<SqlError>().Any(e => NumerosSinConexion.Contains(e.Number))))
                return true;
        return false;
    }
}
