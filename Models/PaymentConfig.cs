using System;

namespace CSharpAssistant.API.Models
{
    public class PaymentConfig
    {
        public int Id { get; set; }

        // Nome da loja exatamente como você usa nos pedidos (Ex.: "Efapi", "Palmital", "Passo dos Fortes")
        public string Store { get; set; } = string.Empty;

        // CNPJ da loja (somente números ou com máscara — você decide)
        public string Cnpj { get; set; } = string.Empty;

        // Provedor: "mercadopago" (padrão agora) ou "pix_banco" (futuro)
        public string Provider { get; set; } = "mercadopago";

        // ---- Credenciais Mercado Pago (uma conta por CNPJ) ----
        public string? MpPublicKey { get; set; }
        public string? MpAccessToken { get; set; }

        // ---- PIX do banco (para futuro) ----
        public string? PixKey { get; set; }         // chave EVP/CNPJ
        public string? BankName { get; set; }       // ex.: "Sicredi", "Itaú"
        public string? BankClientId { get; set; }
        public string? BankClientSecret { get; set; }
        public string? BankCertPath { get; set; }   // se o banco exigir certificado mTLS
        public string? BankCertPassword { get; set; }
        // ---- WhatsApp Cloud API (opcional) ----
        public string? WhatsappStoreNumber { get; set; }
        public string? WhatsappPhoneNumberId { get; set; }
        public string? WhatsappAccessToken { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
