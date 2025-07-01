# 📦 Projeto E-commerce Eskimó - Resumo Completo

## ✅ Status Atual do Sistema

- **Backend:** .NET 8 + PostgreSQL (Render)
- **Frontend Admin:** React + Vite (Vercel)
- **Banco de Dados Atual:**
  - **Host:** dpg-d1668vgdl3ps73fgkqe0-a.oregon-postgres.render.com
  - **Database:** eskimo_db_oobc
  - **Username:** eskimo_user
  - **SSL Mode:** Require + Trust Server Certificate
- **Módulos Finalizados:**
  - Login Admin ✅
  - Cadastro, edição e exclusão de produtos ✅
  - Controle de estoque por loja (Efapi, Palmital, Passo dos Fortes) ✅
  - Visibilidade automática por estoque ✅
- **Em andamento:** Módulo de pedidos (erro 400 no POST /api/orders)

---

## 🔐 Usuário de login ativo para testes

- **Email:** admin@eskimo.com
- **Senha:** admin123 (hash bcrypt salvo corretamente no banco atual)

---

## 📝 Próximos Módulos (planejamento)

1. 📦 **Pedidos**
   - Finalizar criação de pedidos no banco
   - Reduzir estoque automaticamente ao comprar
   - Exibir pedidos no admin por loja

2. 🔢 **Relatórios PDF**
   - Exportação de relatórios de pedidos e estoque
   - Integração com QuestPDF

3. 💳 **Integração PIX**
   - Geração automática de QR Codes PIX
   - Confirmação de pagamento no admin

---

## ⚙️ Tecnologias usadas

- **Backend:** C# .NET 8 WebAPI
- **ORM:** Entity Framework Core + Npgsql
- **Frontend Admin:** React + Vite + Tailwind + Shadcn/ui
- **Banco de dados:** PostgreSQL Render
- **Auth:** JWT Token

---

## 🛠️ Comandos úteis no projeto

- **Rodar API local:**

```bash
dotnet run


Gerar hash bcrypt para senhas:
using BCrypt.Net;
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("suaSenhaAqui"));
📂 Estrutura de pastas (resumo)

CSharpAssistant.API/
├── Controllers/
│   ├── ProductsController.cs
│   ├── OrdersController.cs
│   └── AuthController.cs
├── Models/
│   ├── Product.cs
│   ├── Order.cs
│   ├── OrderItem.cs
│   └── User.cs
├── Data/
│   └── AppDbContext.cs
├── Services/
│   └── TokenService.cs
├── Scripts/
│   └── ImportProductsFromJson.cs
└── Program.cs
🔎 Observação Final

Este arquivo é o resumo principal do projeto Eskimó.
Use em cada novo chat ou upload no ChatGPT para garantir que ele sempre saiba:

Banco correto
Status do sistema
Próximas tarefas
Tecnologias e estrutura
✉️ Preparado por: ChatGPT + Guilherme Tebaldi
🗓 Atualizado em: 01/07/2025

