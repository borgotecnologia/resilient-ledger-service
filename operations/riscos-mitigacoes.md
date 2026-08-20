# Matriz de Gestão de Riscos e Mitigações Técnicas
## Projeto: Resilient Ledger (CashFlow)
## Categoria: Operações e Arquitetura (/operations)

Este documento atende formalmente ao requisito de **Gestão de Riscos e Mitigações** estabelecido no edital de avaliação do Banco. Ele descreve detalhadamente os riscos técnicos, arquiteturais e operacionais identificados na solução de controle de fluxo de caixa, acompanhados de seus respectivos planos de mitigação, estratégias de recuperação (RTO/RPO) e justificativas técnicas de trade-off.

---

## 1. Escopo e Governança de Riscos
A solução de fluxo de caixa (Resilient Ledger) foi desenhada sob os pilares de **alta disponibilidade, resiliência física e segurança por design**. Sendo um sistema financeiro transacional, o maior ativo da companhia é a integridade física do registro de lançamentos (escrita/receita). 

A separação de responsabilidades em microsserviços desacoplados por mensageria orientada a eventos permite isolar falhas, de modo que a degradação de um serviço periférico (como o Consolidado) não resulte em paralisação operacional da entrada de capital.

---

## 2. Matriz de Riscos Arquiteturais e Operacionais

### 🚨 Risco 01: Indisponibilidade Parcial do RabbitMQ (Mensageria)
* **Tipo:** Falha de Comunicação e Indisponibilidade de Componente.
* **Impacto:** Alto (Interrupção na sincronização automática em tempo real entre o banco de escrita e o de leitura).
* **Descrição do Cenário:** O container ou servidor do RabbitMQ sofre uma queda física, perda de conectividade de rede ou exaustão de memória, impedindo que a API de Lançamentos publique novos eventos de débito e crédito na fila.
* **Plano de Mitigação (Fallback Graceful):** 
  * A API de Lançamentos implementa um padrão de resiliência interna: caso a publicação do evento no RabbitMQ falhe por timeout ou conexão recusada, o lançamento é **salvo soberanamente e de forma síncrona** no banco SQL Server transacional.
  * O sistema retorna o status `201 Created` para o lojista, sem interromper o fluxo de vendas (atendimento ao Requisito Não Funcional nº 1).
  * Um log estruturado de nível `Warning` é gerado para a esteira de observabilidade, e a consistência eventual será resolvida de forma reativa assim que o broker retornar ao estado funcional ("Up").

---

### 🚨 Risco 02: Pontos Únicos de Falha (SPOF) do Banco de Escrita (SQL Server)
* **Tipo:** Ponto Único de Falha (SPOF).
* **Impacto:** Crítico (Paralisação total do registro de novos lançamentos).
* **Descrição do Cenário:** O banco SQL Server (On-Premises ou Cloud) sofre falha de hardware, corrupção de disco ou indisponibilidade total, impossibilitando que a API de Lançamentos realize novas operações de escrita.
* **Plano de Mitigação & Recuperação:**
  * **Topologia de Alta Disponibilidade (HA):** Implementação de grupos de disponibilidade (Always On Availability Groups) com replicação síncrona para um nó secundário em zona de disponibilidade distinta.
  * **Failover Automático:** Configuração de failover automático para minimizar a janela de indisponibilidade a poucos segundos.
  * **Read-Only Offloading:** Configuração do nó secundário como somente leitura para absorver consultas analíticas pesadas, blindando o nó primário contra contenção de recursos.

---

### 🚨 Risco 03: Perda de Mensagens (Message Loss) na Fila do Broker
* **Tipo:** Perda de Mensagens / Falha de Consistência.
* **Impacto:** Alto (Divergência financeira permanente entre o saldo do consolidado diário e os lançamentos físicos).
* **Descrição do Cenário:** O broker de mensageria sofre uma queda antes que o consumidor processe a mensagem, resultando na perda definitiva do evento se ele estiver armazenado apenas em memória volátil.
* **Plano de Mitigação Técnica:**
  * **Filas Duráveis e Mensagens Persistentes:** Configuração das filas no RabbitMQ como `Durable` e publicação de mensagens com o modo de entrega persistente (gravação física em disco).
  * **Publisher Confirms:** A API de Lançamentos utiliza confirmações de publicação do RabbitMQ (Publisher Confirms) antes de considerar o fluxo de publicação concluído.
  * **Acknowledge Manual (Manual Ack):** O worker de consumo do Consolidado só envia o `BasicAck` para o RabbitMQ *após* garantir que o saldo foi atualizado e persistido com sucesso no MongoDB. Caso o processamento falhe, a mensagem retorna para a fila de forma automática (`Nack` / Requeue).

---

### 🚨 Risco 04: Entrega Duplicada de Mensagens (At-least-once Delivery)
* **Tipo:** Duplicidade de Mensagens.
* **Impacto:** Alto (Fraude ou erro contábil de saldo duplicado).
* **Descrição do Cenário:** Devido a instabilidades temporárias de rede, o consumidor atualiza o MongoDB com sucesso, mas a rede cai antes de enviar o `Ack` ao RabbitMQ. O broker re-enfileira a mensagem e a entrega novamente para processamento.
* **Plano de Mitigação (Garantia de Idempotência):**
  * O payload de todo evento publicado no RabbitMQ obrigatoriamente contém o **ID único universal (GUID)** gerado originalmente no banco de lançamentos SQL Server.
  * O microsserviço de Consolidado, antes de acumular o valor no saldo diário, realiza uma operação atômica de validação ou rastreamento no MongoDB para checar se o ID do lançamento em questão já foi computado naquele dia.
  * Se o ID já existir no histórico de reconciliação de mensagens do dia, o evento duplicado é descartado silenciosamente com envio de `Ack` manual ao broker, evitando duplicidade financeira.

---

### 🚨 Risco 05: Gargalo e Atraso no Processamento de Mensagens (Backpressure)
* **Tipo:** Atraso de Eventos / Lag de Consistência.
* **Impacto:** Médio (O lojista vê o saldo desatualizado por vários minutos na API de Consolidado).
* **Descrição do Cenário:** Em picos extremos de lançamentos concorrentes, a quantidade de mensagens geradas é infinitamente maior do que a capacidade de consumo de uma única instância do worker de Consolidado, gerando um "lag" acentuado.
* **Plano de Mitigação:**
  * **Escalonamento Horizontal de Consumidores (Competing Consumers):** O microsserviço de Consolidado é desenhado para permitir escalonamento horizontal (múltiplas réplicas idênticas do worker rodando simultaneamente). O RabbitMQ distribui as mensagens de forma balanceada (Round-Robin) entre as instâncias concorrentes.
  * **Tuning de Pre-fetch Count:** Limitação do número de mensagens simultâneas não-confirmadas por worker (ex: `prefetch = 50`) para evitar sobrecarga de memória em uma única réplica e maximizar a taxa de vazão (*throughput*).

---

### 🚨 Risco 06: Concorrência e Gravações Simultâneas no Saldo Consolidado
* **Tipo:** Concorrência e Inconsistência de Dados.
* **Impacto:** Médio a Alto (Cálculo incorreto do saldo decorrente de condições de corrida - Race Conditions).
* **Descrição do Cenário:** Múltiplas instâncias do consumidor processam lançamentos distintos referentes ao mesmo dia ao mesmo tempo. Duas instâncias tentam ler, atualizar e salvar o saldo diário do MongoDB de forma concorrente, resultando na perda de um dos lançamentos (*Lost Update*).
* **Plano de Mitigação:**
  * **Operações Atômicas de Incremento (NoSQL):** O consumidor do Consolidado não realiza operações do tipo "Leitura + Cálculo em Memória + Escrita". Em vez disso, utiliza operadores de incremento atômico nativos do MongoDB (ex: `$inc` em conjunto com `findOneAndUpdate`).
  * O MongoDB garante que o incremento ocorre sob lock interno de documento, blindando o saldo consolidado contra qualquer inconsistência gerada por processamento paralelo e garantindo consistência matemática rigorosa.

---

### 🚨 Risco 07: Riscos de Segurança de APIs e Redes Híbridas
* **Tipo:** Segurança e Vazamento de Dados Fiscais/Financeiros.
* **Impacto:** Crítico (Vazamento de dados, exposição a ataques comuns, violação de conformidade LGPD).
* **Descrição do Cenário:** Exposição de endpoints sensíveis de consulta de saldo, intercepção de tráfego de dados financeiros entre as APIs ou ataques de negação de serviço (DDoS/Brute Force).
* **Plano de Mitigação (Security by Design):**
  * **API Gateway & Rate Limiting:** Centralização de todas as chamadas externas no API Gateway, aplicando políticas rígidas de limite de requisições por IP (*Rate Limit*) e *throttling* para amortecer ataques.
  * **Segurança na Rede Híbrida (VPN/mTLS):** A comunicação entre o cluster Kubernetes na Nuvem Pública (onde rodam as APIs e o Mongo/RabbitMQ) e o Data Center local (SQL Server de Lançamentos) trafega obrigatoriamente sob tunelamento seguro IPSec VPN ou canal dedicado ExpressRoute, blindando o tráfego com criptografia em trânsito (TLS 1.2+).
  * **Autenticação Descentralizada (JWT/OAuth2):** Autenticação stateless de ponta a ponta. Todas as requisições aos endpoints privados exigem um token Bearer JWT assinado digitalmente e validado nas controllers antes de qualquer leitura ou persistência.

---

## 3. Diretrizes de Continuidade de Negócio (RTO & RPO)

Para alinhar as expectativas operacionais do negócio aos requisitos técnicos da solução, a arquitetura estabelece as seguintes métricas de recuperação de desastres:

| Componente de Negócio | RTO (Recovery Time Objective) | RPO (Recovery Point Objective) | Estratégia de Sustentação |
| :--- | :---: | :---: | :--- |
| **API de Lançamentos (Escrita/Receita)** | **< 1 minuto** | **0 (Zero data loss tolerado)** | Grupos de Disponibilidade Ativa (SQL Server AlwaysOn Availability Groups) + Replicação Síncrona. |
| **Fila de Mensageria (RabbitMQ)** | **< 10 minutos** | **< 10 segundos** | Filas duráveis baseadas em Quorum Queues (RabbitMQ Quorum Queues), distribuídas de forma redundante em cluster multinó. |
| **API de Consolidado (Relatório/Leitura)** | **< 15 minutos** | **Até 1 minuto (Consistência Eventual)** | Replica Set do MongoDB em nuvem secundária. Aceite estratégico do risco de consistência eventual por alguns segundos após grandes rajadas de transação. |

---

## 4. Estratégia de Monitoramento e Observabilidade (Operações)

A operação em produção se baseia em métricas quantitativas estruturadas para evitar que desvios silenciosos cheguem ao usuário:

1. **Monitoramento do Broker (RabbitMQ):**
   * Alertas automáticos baseados no tamanho da fila (`Queue Depth`). Se o volume de mensagens pendentes em `CashFlow.Lancamentos.Registrados` exceder 1.000 mensagens, um alerta de severidade alta é disparado (indicando falha no worker do Consolidado).
   * Alerta de `Unacknowledged Messages`. Indica que workers estão consumindo, mas falhando na finalização dos processos.
2. **Monitoramento de Banco de Dados:**
   * Alertas para conexões pendentes e pools saturados no SQL Server e MongoDB.
   * Rastreabilidade de requisições pesadas via logs estruturados contendo `Correlation ID` transversal desde o API Gateway até a base NoSQL.
3. **Métricas de Performance da API (SLAs):**
   * Acompanhamento rígido do tempo médio de resposta para a API de Consolidado (relatório de leitura) para garantir conformidade com o pico estimado de 50 requisições por segundo sob no máximo 5% de perda (SLO estabelecido no edital).
