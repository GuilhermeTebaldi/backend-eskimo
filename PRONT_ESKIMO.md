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
🔹 3. Estrutura de Pastas (resumo)
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
🔹 4. Comandos Essenciais
# Gerar nova migration
dotnet ef migrations add NomeDaAlteracao --project e-commerce.csproj

# Aplicar migration no banco remoto (Render)
dotnet ef database update --project e-commerce.csproj

# Compilar projeto
dotnet build e-commerce.csproj

# Rodar local em modo produção
ASPNETCORE_ENVIRONMENT=Production dotnet run --project e-commerce.csproj

# Listar migrations aplicadas/local
dotnet ef migrations list --project e-commerce.csproj
🔹 5. Comandos SQL Úteis
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
🔹 6. Convenções Aprendidas
Erro	Causa	Correção
MSB1009	Nome errado do .csproj	Usar e-commerce.csproj
Banco localhost	appsettings.Development.json sobrescreve	Executar com ASPNETCORE_ENVIRONMENT=Production
AmbiguousMatchException	Endpoint duplicado	Um único controller por rota
PendingModelChangesWarning	Modelo ≠ snapshot	Criar migration antes de database update
zsh: parse error	JSON direto no terminal	Criar arquivo .json e usar --data-binary
🔹 7. Sequência para Nova Feature
Rodar build e migrations locais.
Testar dotnet run com banco remoto.
Validar endpoints via curl.
Commit e push no GitHub.
Render redeploya e aplica migrations.
Testar /swagger e o painel Admin.
🔹 8. Política de CORS
Adicionar novos domínios em Program.cs:
policy.WithOrigins(
  "https://admin.eskimochapeco.com.br",
  "https://eskimochapeco.com.br",
  "https://site-eskimo.vercel.app"
);
🔹 9. Backup e Versionamento
Criar backup:
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd pg_dump \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user eskimo_db_oobc_7nxm > backup_eskimo.sql
Restaurar backup:
PGPASSWORD=3pZjAaQgw1za3eBZpM2OiOMtePUOGNGd psql \
-h dpg-d2524ovdiees739r0mn0-a.oregon-postgres.render.com \
-U eskimo_user -d eskimo_db_oobc_7nxm < backup_eskimo.sql
🔹 10. Como Iniciar uma Nova Alteração
Antes de qualquer pedido, envie:
🚀 PRONT ESKIMÓ ATUALIZAÇÃO 🚀

Banco: Render (eskimo_db_oobc_7nxm)
Projeto: e-commerce.csproj
ConnectionStrings__Default configurado
API: CSharpAssistant.API/
Frontend: admin-panel/
Ambiente: Production

Objetivo da mudança: (descreva aqui)
Arquivos principais: (liste aqui)
🔹 11. Configuração de SDK e EF CLI (Evita 90 % dos travamentos)
# Garantir SDK compatível com global.json
jq '.sdk.version="9.0.201"' /Users/admin/Documents/e-commerce/global.json > /tmp/g && mv /tmp/g /Users/admin/Documents/e-commerce/global.json

# Adicionar ferramentas EF ao PATH
export PATH="$PATH:/Users/admin/.dotnet/tools"

# Atualizar EF CLI
dotnet tool update --global dotnet-ef
dotnet ef --info
💡 Resultado: nenhuma falha “SDK não encontrado” ou “ef não existe”.
🔹 12. Estrutura de Commits e Patches
Padronizar commits:
feat: nova função (ex: arquivar produto)
fix: correção (ex: salvar estoque)
refactor: limpeza (ex: reordenação de imports)
Ao solicitar mudança, liste:
Arquivos afetados:
- CSharpAssistant.API/Controllers/ProductsController.cs
- admin-panel/src/pages/EstoquePorLoja.jsx
→ facilita gerar patches exatos linha a linha.
🔹 13. Sincronização Frontend ↔ Backend (Fluxo seguro de deploy)
Verificar se há nova migration (dotnet ef migrations list).
Subir primeiro o backend (Render aplica migration).
Só depois subir admin (npm run build && git push).
Validar /swagger e painel.
💡 Evita erros “coluna inexistente” durante requisições.
🔹 14. Checklist Final Antes do Deploy
✅ Build sem erro (dotnet build).
✅ Migration aplicada (dotnet ef database update).
✅ Commit limpo (git status).
✅ Testes via curl ou Swagger.
✅ Admin testado local (npm run dev).
✅ Push em main → Render aplica automaticamente.
