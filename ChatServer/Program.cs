using ChatServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5280", "https://localhost:7252");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IContactService, ContactService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<IMessageBrokerService, MessageBrokerService>();
builder.Services.AddSingleton<ISessionKeyService, SessionKeyService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/", () => "ChatServer is running!");

app.Run();

public partial class Program { }
