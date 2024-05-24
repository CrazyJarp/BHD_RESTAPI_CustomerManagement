using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Define la clase ClientesController como un controlador de API
[ApiController]
[Route("bhd/api/v1/personales/[controller]")]
public class ClientesController : ControllerBase
{
    // Campos privados para el IHttpClientFactory, ILogger y IConfiguration
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ClientesController> _logger;
    private readonly IConfiguration _configuration;

    // Constructor que inicializa los campos privados
    public ClientesController(IHttpClientFactory httpClientFactory, ILogger<ClientesController> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    // Método GET que maneja solicitudes HTTP GET
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string id_transaccion, [FromQuery] string canal, [FromQuery] string num_doc)
    {
        // Validación de id_transaccion: debe ser un UUID válido
        if (!Guid.TryParse(id_transaccion, out var transactionId))
            return BadRequest("id_transaccion no valido. Debe ser un Guid valido.");

        // Validación de canal: debe tener 3 caracteres en mayúsculas
        if (string.IsNullOrEmpty(canal) || canal.Length != 3 || canal != canal.ToUpper())
            return BadRequest("Formato de canal no valido. Deben tener 3 caracteres en mayúscula.");

        // Validación de num_doc: debe tener 12 dígitos
        if (string.IsNullOrEmpty(num_doc) || num_doc.Length != 12 || !long.TryParse(num_doc, out _))
            return BadRequest("Formato num_doc no válido. Debe tener 12 dígitos.");

        // Obtener token OAuth
        var token = await GetOAuthToken();
        if (string.IsNullOrEmpty(token))
        {
            return StatusCode(500, "Failed to retrieve OAuth token.");
        }

        // Crear cliente HTTP para la API externa
        var client = _httpClientFactory.CreateClient("ExternalApi");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Hacer la llamada a la API externa
        var response = await client.GetAsync($"info?transactionId={transactionId}&channel={canal}&documentNumber={num_doc}");

        // Manejo de error si la llamada a la API externa falla
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"External API call failed with status code {response.StatusCode}, Response: {errorContent}");
            return StatusCode((int)response.StatusCode, "Error calling external API.");
        }

        // Leer y deserializar la respuesta de la API externa
        var content = await response.Content.ReadAsStringAsync();
        var externalResponse = JsonSerializer.Deserialize<ExternalCustomerResponse>(content);

        // Enmascarar el email en la respuesta
        externalResponse.Email = MaskEmail(externalResponse.Email);

        // Devolver la respuesta OK con los datos obtenidos
        return Ok(externalResponse);
    }

    // Método POST que maneja solicitudes HTTP POST
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateCustomerRequest request)
    {

        // Validación de id_transaccion: debe ser un UUID válido
        if (!Guid.TryParse(request.TransactionId, out var transactionId))
            return BadRequest("id_transaccion no valido. Debe ser un Guid valido.");

        // Validación de canal: debe tener 3 caracteres en mayúsculas
        if (string.IsNullOrEmpty(request.Channel) || request.Channel.Length != 3 || request.Channel != request.Channel.ToUpper())
            return BadRequest("Formato de canal no valido. Deben tener 3 caracteres en mayúscula.");

        // Validación de num_doc: debe tener 12 dígitos
        if (string.IsNullOrEmpty(request.DocumentNumber) || request.DocumentNumber.Length != 12 || !long.TryParse(request.DocumentNumber, out _))
            return BadRequest("Formato num_doc no válido. Debe tener 12 dígitos.");


        // Obtener token OAuth
        var token = await GetOAuthToken();
        if (string.IsNullOrEmpty(token))
        {
            return StatusCode(500, "Failed to retrieve OAuth token.");
        }

        // Crear cliente HTTP para la API externa
        var client = _httpClientFactory.CreateClient("ExternalApi");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Hacer la llamada POST a la API externa
        var response = await client.PostAsJsonAsync("info", request);

        // Manejo de error si la llamada a la API externa falla
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"External API call failed with status code {response.StatusCode}, Response: {errorContent}");
            return StatusCode((int)response.StatusCode, "Error calling external API.");
        }

        // Devolver el estado 201 Created
        return StatusCode(201);
    }

    // Método privado para obtener el token OAuth
    private async Task<string> GetOAuthToken()
    {
        using var client = _httpClientFactory.CreateClient();

        // Obtener la URL del token y las credenciales del cliente desde la configuración
        var tokenUrl = _configuration["OAuth2:TokenUrl"];
        var clientId = _configuration["OAuth2:ClientId"];
        var clientSecret = _configuration["OAuth2:ClientSecret"];

        // Crear la solicitud HTTP para obtener el token
        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", "customers")
        });

        // Enviar la solicitud y obtener la respuesta
        var response = await client.SendAsync(request);

        // Manejo de error si la solicitud de token falla
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to get OAuth token. Status Code: {response.StatusCode}, Response: {errorContent}");
            return null; // Devolver null o lanzar una excepción según la estrategia de manejo de errores
        }

        // Leer y deserializar la respuesta del token
        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(content);

        // Devolver el token de acceso
        return tokenResponse.AccessToken;
    }

    // Método privado para enmascarar el email
    private string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;

        var namePart = parts[0];
        var domainPart = parts[1];

        var maskedName = namePart.Length > 3 ? namePart.Substring(0, 3) + new string('*', namePart.Length - 3) : namePart;
        var maskedDomain = domainPart.Length > 3 ? domainPart.Substring(0, 3) + new string('*', domainPart.Length - 3) : domainPart;

        return $"{maskedName}@{maskedDomain}";
    }
}

// Clase que representa la solicitud para crear un cliente
public class CreateCustomerRequest
{
    [Required]
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; }

    [Required]
    [JsonPropertyName("channel")]
    public string Channel { get; set; }

    [Required]
    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; }

    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [Required]
    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    [Required]
    [JsonPropertyName("phone")]
    public string Phone { get; set; }

    [Required]
    [JsonPropertyName("email")]
    public string Email { get; set; }
}

// Clase que representa la respuesta del token OAuth
public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

// Clase que representa la respuesta de la API externa sobre el cliente
public class ExternalCustomerResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
 
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; }

}
