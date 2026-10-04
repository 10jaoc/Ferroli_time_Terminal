using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FerroliTime.Terminal.Services;

namespace FerroliTime.Terminal.Pages;

[IgnoreAntiforgeryToken]
public class ErrorModel(IWebHostEnvironment env) : PageModel
{
    public string Mensaje { get; private set; } = "No se ha podido completar la operación. Inténtelo de nuevo en unos instantes.";
    public string? Detalle { get; private set; }

    public void OnGet() => CargarDetalle();
    public void OnPost() => CargarDetalle();

    private void CargarDetalle()
    {
        var error = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (ErroresBd.EsSinConexion(error)) Mensaje = ErroresBd.MensajeSinConexion;
        if (env.IsDevelopment()) Detalle = error?.ToString();
    }
}
