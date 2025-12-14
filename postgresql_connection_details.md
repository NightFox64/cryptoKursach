
**Для ChatServer:**
*   **Файл:** `ChatServer\appsettings.json`
*   **Строка подключения:**
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Port=5432;Database=chatserver_db;Username=postgres;Password=Ichiho64"
    }
    ```
    *   **Примечание:** Вам нужно будет заменить `localhost`, `5432`, `chatserver_db`, `postgres` и `Ichiho64` на фактические данные вашего сервера PostgreSQL.

**Для ChatClient:**
*   **Файл:** `ChatClient\Data\ApplicationDbContext.cs`
*   **Строка подключения:**
    ```csharp
    optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=chatclient_db;Username=postgres;Password=Ichiho64");
    ```
    *   **Примечание:** Вам нужно будет заменить `localhost`, `5432`, `chatclient_db`, `postgres` и `Ichiho64` на фактические данные вашего сервера PostgreSQL.

**Чтобы все заработало, вам нужно будет:**
1.  **Установить PostgreSQL:** Убедитесь, что у вас установлен и запущен сервер PostgreSQL.
2.  **Создать базы данных:** Создайте две новые базы данных на вашем сервере PostgreSQL: `chatserver_db` и `chatclient_db` (или используйте другие имена, но не забудьте обновить строки подключения).
3.  **Обновить пароль:** Замените `your_password` на фактический пароль пользователя `postgres` (или другого пользователя, которого вы используете) в строках подключения.
4.  **Применить миграции (для ChatServer):** Если вы используете миграции Entity Framework Core, вам нужно будет создать новую миграцию и применить ее для `ChatServer`, чтобы создать схему базы данных в PostgreSQL.
    *   Откройте терминал в папке `ChatServer`.
    *   `dotnet ef migrations add InitialPostgreMigration`
    *   `dotnet ef database update`
5.  **Запустить приложение:** Запустите `ChatServer` и `ChatClient`. При первом запуске `ChatClient` он должен создать свою базу данных автоматически.
