using APIHU.Application.Interfaces;

namespace APIHU.Application.Services;

/// <summary>
/// Servicio para gestionar los prompts de IA
/// </summary>
public class PromptService : IPromptService
{
    private readonly Dictionary<string, Dictionary<string, string>> _promptsPorVersion;
    private readonly ILogger<PromptService> _logger;

    public PromptService(ILogger<PromptService> logger)
    {
        _logger = logger;
        _promptsPorVersion = new Dictionary<string, Dictionary<string, string>>();
        CargarPrompts();
    }

    private void CargarPrompts()
    {
        var basePath = AppContext.BaseDirectory;
        var promptsPath = Path.Combine(basePath, "Prompts");

        if (!Directory.Exists(promptsPath))
        {
            _logger.LogWarning("No se encontró el directorio de prompts en {Path}", promptsPath);
            CargarPromptsPorDefecto();
            return;
        }

        var versiones = Directory.GetDirectories(promptsPath);

        foreach (var versionDir in versiones)
        {
            var version = Path.GetFileName(versionDir);
            var prompts = new Dictionary<string, string>();

            // Cargar cada tipo de prompt
            var archivos = new[] { "limpieza.txt", "estructuracion.txt", "hu.txt" };
            foreach (var archivo in archivos)
            {
                var filePath = Path.Combine(versionDir, archivo);
                if (File.Exists(filePath))
                {
                    prompts[archivo.Replace(".txt", "")] = File.ReadAllText(filePath);
                    _logger.LogInformation("Cargado prompt {Tipo} versión {Version}", archivo, version);
                }
            }

            if (prompts.Count > 0)
            {
                _promptsPorVersion[version] = prompts;
            }
        }

        if (_promptsPorVersion.Count == 0)
        {
            _logger.LogWarning("No se cargaron prompts, usando valores por defecto");
            CargarPromptsPorDefecto();
        }
    }

    private void CargarPromptsPorDefecto()
    {
        _promptsPorVersion["v1"] = new Dictionary<string, string>
        {
            ["limpieza"] = "Lee el siguiente texto y reescríbelo sin errores, sin repeticiones y sin información innecesaria. Solo devuelve el texto limpio:\n\n{texto}",
            ["estructuracion"] = "Del siguiente texto, extrae los requerimientos o necesidades en una lista simple:\n\n{texto}",
            ["hu"] = "Del siguiente texto de reunión, crea {maxHUs} historias de usuario en español ({idioma}).\n\nFormato obligatorio:\nHU1: Como [rol], quiero [qué necesita], para [beneficio].\nCriterios: 1)..., 2)...\n\nUsa estos requerimientos:\n{requerimientos}\n\nY este texto:\n{texto}"
        };
    }

    public string ObtenerPromptLimpieza(string version = "v1")
    {
        if (_promptsPorVersion.TryGetValue(version, out var prompts))
        {
            if (prompts.TryGetValue("limpieza", out var prompt))
            {
                return prompt;
            }
        }

        _logger.LogWarning("No se encontró prompt de limpieza para versión {Version}, usando v1", version);
        return _promptsPorVersion["v1"]["limpieza"];
    }

    public string ObtenerPromptEstructuracion(string version = "v1")
    {
        if (_promptsPorVersion.TryGetValue(version, out var prompts))
        {
            if (prompts.TryGetValue("estructuracion", out var prompt))
            {
                return prompt;
            }
        }

        _logger.LogWarning("No se encontró prompt de estructuración para versión {Version}, usando v1", version);
        return _promptsPorVersion["v1"]["estructuracion"];
    }

    public string ObtenerPromptHU(string version = "v1")
    {
        if (_promptsPorVersion.TryGetValue(version, out var prompts))
        {
            if (prompts.TryGetValue("hu", out var prompt))
            {
                return prompt;
            }
        }

        _logger.LogWarning("No se encontró prompt de HU para versión {Version}, usando v1", version);
        return _promptsPorVersion["v1"]["hu"];
    }

    public List<string> ObtenerVersionesDisponibles()
    {
        return _promptsPorVersion.Keys.ToList();
    }
}