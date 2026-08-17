# Resilient Ledger Service

## 🎯 Business Context & Value Proposition
Este projeto apresenta uma arquitetura de missão crítica desenhada para o gerenciamento de **Fluxo de Caixa** de comerciantes. O foco central não é apenas o registro de transações, mas a garantia de que o negócio nunca pare, mantendo a integridade financeira e a visibilidade dos saldos diários.

A solução foi projetada sob o paradigma de **Arquitetura Orientada a Eventos (EDA)**, garantindo que a operação vital (lançamentos) seja resiliente a falhas em serviços de suporte (relatórios consolidadores).

## 📊 Business Capabilities (Mapa de Capacidades)
Para atender aos objetivos estratégicos, a arquitetura habilita as seguintes capacidades:
* **Ledger Management:** Registro confiável de lançamentos de débito e crédito.
* **Daily Financial Insight:** Disponibilização de saldo consolidado diário para tomada de decisão.
* **Operational Resilience:** Garantia de operação ininterrupta do caixa, mesmo sob indisponibilidade parcial de sistemas de BI/Relatórios.

## 📋 Requirements Refinement (Levantamento de Requisitos)

### Functional Requirements
1. **Lançamentos:** O sistema deve permitir o registro de transações financeiras (débito/crédito) com data e valor.
2. **Consolidação:** O sistema deve gerar e permitir a consulta de um relatório de saldo diário consolidado.

### Non-Functional Requirements (Critical for Architect evaluation)
1. **Decoupled Availability:** O serviço de lançamentos **não deve** ficar indisponível caso o serviço de consolidado falhe.
2. **High Throughput:** A arquitetura deve suportar picos de **50 requisições por segundo** no serviço de consolidado, com uma taxa de perda máxima de apenas 5%.
3. **Security by Design:** Toda a comunicação e acesso aos dados devem ser protegidos por padrões de **Autenticação e Autorização (OAuth2/JWT)**.
