# ADR 0004: Estratégia de Modernização e Modelo de Infraestrutura Híbrida

## Status
Aceito

## Contexto (O Problema de Negócio)
Conforme o descritivo da solução, o novo sistema de controle de fluxo de caixa deve coexistir ou substituir processos existentes. O desafio de negócio é garantir que a nova solução suporte o crescimento de volume de 50 requisições por segundo sem descartar abruptamente investimentos já realizados em hardware ou sistemas contábeis locais, minimizando o risco operacional durante a virada de chave.

## Decisão (A Solução Estratégica)
Adotamos uma abordagem de Arquitetura Híbrida baseada no padrão de Modernização via Estrangulamento (Strangler Fig Pattern):

1. Aproveitamento do Legado: O sistema contábil existente será mantido como a fonte da verdade fiscal e de back-office. O novo sistema funcionará como um motor de processamento rápido que envia dados consolidados para o legado, evitando sobrecarga no ambiente antigo.
2. Modelo Híbrido de Deploy: A camada de persistência transacional (SQL Server) será mantida em infraestrutura dedicada para garantir soberania de dados, enquanto as APIs e o banco de relatórios (MongoDB) serão implantados em ambiente de nuvem elástica utilizando containers para suportar os picos de tráfego exigidos.

## Consequências e Justificativa de Investimento
* Redução de Risco: Ao adotar uma transição gradual, evitamos uma migração do tipo "big bang". O novo sistema assume as novas operações enquanto o legado é estrangulado aos poucos, garantindo que o comerciante nunca pare de operar.
* Equilíbrio Financeiro: Maximizamos o retorno sobre o investimento (ROI) ao aproveitar o hardware já pago para os dados históricos, utilizando o modelo de custo variável da nuvem (OPEX) apenas para a escala necessária dos novos serviços.
* Escalabilidade Focada: Esta estratégia permite escalar a infraestrutura de relatórios de forma independente, atendendo ao requisito de performance sem elevar desnecessariamente o custo de licenciamento ou hardware do núcleo transacional.
