using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace APIHU.Infrastructure.Formatters;

/// <summary>
/// Input formatter que permite a controllers ASP.NET Core aceptar un body
/// de tipo <c>text/plain</c> y enlazarlo directamente a un parámetro string
/// con <c>[FromBody]</c>. Sin esto, ASP.NET Core solo entiende JSON por defecto.
///
/// Imprescindible para el endpoint POST /api/hu/generate-from-text, donde el
/// body es la transcripción cruda (con saltos de línea reales).
/// </summary>
public class TextPlainInputFormatter : TextInputFormatter
{
    public TextPlainInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/plain"));
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanReadType(Type type) => type == typeof(string);

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context,
        Encoding effectiveEncoding)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(effectiveEncoding);

        using var reader = new StreamReader(context.HttpContext.Request.Body, effectiveEncoding);
        var texto = await reader.ReadToEndAsync();
        return await InputFormatterResult.SuccessAsync(texto);
    }
}
