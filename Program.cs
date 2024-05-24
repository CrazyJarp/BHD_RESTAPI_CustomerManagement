using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http.Headers;

// Crear el constructor del WebApplication con los argumentos de línea de comando
var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor de dependencias
builder.Services.AddControllers(); // Agrega los controladores como servicios para que puedan ser inyectados
builder.Services.AddEndpointsApiExplorer(); // Agrega soporte para la exploración de endpoints de la API
builder.Services.AddSwaggerGen(); // Agrega soporte para la generación de documentación Swagger

// Configurar el cliente HTTP para la API externa
builder.Services.AddHttpClient("ExternalApi", client =>
{
    // Establecer la base URI para el cliente HTTP
    client.BaseAddress = new Uri("https://api-dev.bhdleon.com.do/bhd/api/v1.3/personal/customers/");
    // Agregar un encabezado de aceptación de contenido JSON
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// Construir la aplicación web
var app = builder.Build();

// Configurar el pipeline de manejo de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    // Usar la página de excepciones del desarrollador en el entorno de desarrollo
    app.UseDeveloperExceptionPage();
    // Habilitar middleware de Swagger para la generación y visualización de la documentación de la API
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar la redirección de HTTP a HTTPS
app.UseHttpsRedirection();

// Habilitar la autorización
app.UseAuthorization();

// Mapear los controladores a las rutas
app.MapControllers();

// Ejecutar la aplicación
app.Run();

// Definición de la clase parcial Program
public partial class Program { }
