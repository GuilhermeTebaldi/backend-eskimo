# 🧩 PRONT ESKIMÓ — CONTEXTO PADRÃO PARA MUDANÇAS

## 🔹 1. Projeto
- Repositório local: `e-commerce/`
- API principal: `CSharpAssistant.API/`
- Projeto .NET principal: `e-commerce.csproj`
- Frontend admin: `admin-panel/`
- Frontend loja: `src/Loja.tsx`

---

## 🔹 2. Banco de Dados (Render)
- Host: `dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com`
- Porta: `5432`
- Database: `eskimo_db_oobc_7nxm`
- Username: `eskimo_user`
- Password: `3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd`

### 📡 Connection String
Host=dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com;
Port=5432;
Database=eskimo_db_oobc_7nxm;
Username=eskimo_user;
Password=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd;
SSL Mode=Require;
Trust Server Certificate=true

### 💾 Variável de ambiente usada nos comandos
```bash
export ConnectionStrings__Default="Host=dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com;Port=5432;Database=eskimo_db_oobc_7nxm;Username=eskimo_user;Password=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd;SSL Mode=Require;Trust Server Certificate=true"
🔹 3. Estrutura de pastas (resumo)
e-commerce/
└── CSharpAssistant.API/
    ├── e-commerce.csproj
    ├── Controllers/
    ├── Models/
    ├── Services/
    ├── Data/
    ├── DTOs/
    ├── Migrations/
    ├── appsettings.json
    ├── Program.cs
    └── backup_eskimo.sql
🔹 4. Comandos essenciais
▶️ Gerar migration
dotnet ef migrations add NomeDaAlteracao --project e-commerce.csproj
🧭 Aplicar no banco remoto (Render)
dotnet ef database update --project e-commerce.csproj
🧱 Compilar
dotnet build e-commerce.csproj
🚀 Rodar local em modo produção
ASPNETCORE_ENVIRONMENT=Production dotnet run --project e-commerce.csproj
🔍 Ver lista de migrações
dotnet ef migrations list --project e-commerce.csproj
🔹 5. Comandos SQL úteis
📋 Ver colunas de uma tabela
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd psql \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user -d eskimo_db_oobc_7nxm \
-c "select column_name from information_schema.columns where table_name='Products' order by column_name;"
📦 Ver histórico de migrações aplicadas
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd psql \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user -d eskimo_db_oobc_7nxm \
-c 'select * from "__EFMigrationsHistory" order by "MigrationId";'
💣 Restaurar backup
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd psql \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user -d eskimo_db_oobc_7nxm < backup_eskimo.sql
🔹 6. Convenções aprendidas
Erro	Causa	Correção
MSB1009	Nome errado do .csproj	Usar e-commerce.csproj
Banco localhost	appsettings.Development.json sobrescreve	Usar ASPNETCORE_ENVIRONMENT=Production
AmbiguousMatchException	Endpoint duplicado	Um único controller por rota
PendingModelChangesWarning	Modelo ≠ snapshot	Criar migration antes de database update
zsh: parse error	JSON direto no terminal	Criar arquivo .json e usar --data-binary
🔹 7. Sequência para nova feature
Rodar build e migrations locais.
Testar dotnet run com banco remoto.
Validar endpoints via curl.
Commit e push.
Render redeploya e aplica migrations.
Testar /swagger e Admin.
🔹 8. Política de CORS
Adicionar novos domínios no Program.cs:
policy.WithOrigins(
  "https://admin.eskimochapeco.com.br",
  "https://eskimochapeco.com.br",
  "https://site-eskimo.vercel.app"
);
🔹 9. Backup e versionamento
Criar backup
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd pg_dump \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user eskimo_db_oobc_7nxm > backup_eskimo.sql
Restaurar
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd psql \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user -d eskimo_db_oobc_7nxm < backup_eskimo.sql
🔹 10. Como iniciar nova alteração
Antes de qualquer pedido, envie no chat:
🚀 PRONT ESKIMÓ ATUALIZAÇÃO 🚀

Banco: Render (eskimo_db_oobc_7nxm)
Projeto: e-commerce.csproj
ConnectionStrings__Default configurado
API: CSharpAssistant.API/
Frontend: admin-panel/
Ambiente: Production

Objetivo da mudança: (descreva aqui)
Arquivos principais: (liste aqui)
Local recomendado:
/Users/admin/Documents/e-commerce/CSharpAssistant.API/PRONT_ESKIMO.md

---

Basta criar o arquivo:
```bash
cat > /Users/admin/Documents/e-commerce/CSharpAssistant.API/PRONT_ESKIMO.md <<'EOF'
(paste the markdown content above)
EOF
