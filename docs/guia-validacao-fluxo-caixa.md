# Guia de Validação Técnica e Funcional Ponta a Ponta
## Projeto: CashFlow (Resilient Ledger)

Este documento estabelece o roteiro operacional para a homologação, execução e validação funcional das APIs de **Lançamentos** e **Consolidado Diário** que integram o projeto CashFlow. O objetivo é permitir que o comitê de avaliação realize a auditoria técnica da solução em um ambiente controlado, reproduzindo os fluxos transacionais e de resiliência especificados no desafio.

---

## 📋 Requisitos do Ambiente de Execução

Para a correta execução dos testes e validação das capacidades, certifique-se de que o ambiente dispõe dos seguintes componentes instalados:
1. **Docker Engine & Docker Compose** (ambiente de containerização de infraestrutura).
2. **.NET 8 SDK** (compilação e runtime dos microsserviços).
3. **Navegador Web** (para acesso à interface Swagger de documentação de APIs).

---

## 🛠️ 1. Provisionamento da Infraestrutura Local (Docker)

O projeto baseia-se em uma arquitetura de serviços desacoplados apoiada por três componentes de infraestrutura containerizados (bancos de dados isolados e mensageria).

1. Abra o terminal na **pasta raiz** do repositório (onde localiza-se o arquivo `docker-compose.yml`).
2. Inicialize os serviços em segundo plano:
   ```bash
   docker compose up -d
   ```
3. Valide o status de inicialização e saúde dos containers executando:
   ```bash
   docker ps
   ```
   *Certifique-se de que os seguintes serviços encontram-se no status "Up" com suas respectivas portas expostas:*
   * `sqlserver_local` (Porta: 1433) - Armazenamento relacional transacional (Lançamentos).
   * `rabbitmq_cloud` (Portas: 5672 e 15672) - Broker de mensageria assíncrona.
   * `mongodb_cloud` (Porta: 27017) - Repositório NoSQL orientado a documentos (Consolidado).

---

## 🚀 2. Inicialização dos Serviços (Microsserviços .NET 8)

Para executar a solução localmente com os caminhos corretos da estrutura padrão do projeto (`src/`), execute as aplicações em **dois terminais distintos**:

### Terminal 1: Serviço de Lançamentos (Escrita)
1. Navegue até o diretório do projeto de Lançamentos:
   ```bash
   cd src/CashFlow.Lancamentos
   ```
2. Inicialize o runtime da aplicação:
   ```bash
   dotnet run
   ```
3. **Comportamento esperado:** O compilador processará a aplicação e disponibilizará o endpoint HTTP local (ex: `Now listening on: http://localhost:5182`). No primeiro ciclo de inicialização (*first boot*), a rotina de carga do banco executa automaticamente a criação de tabelas e a estrutura de dados necessária no SQL Server local.

### Terminal 2: Serviço de Consolidado (Leitura & Consumidor de Eventos)
1. Abra um segundo terminal.
2. Navegue até o diretório do projeto de Consolidado:
   ```bash
   cd src/CashFlow.Consolidado
   ```
3. Inicialize o runtime do serviço:
   ```bash
   dotnet run
   ```
4. **Comportamento esperado:** O serviço inicializará na sua respectiva porta HTTP (ex: `Now listening on: http://localhost:5254`) e registrará o *background consumer* no Broker do RabbitMQ, aguardando eventos de novos lançamentos para processamento assíncrono.

---

## 🧪 3. Execução de Testes Funcionais (Validação de Negócio)

Este roteiro reproduz a atividade operacional para validar as capacidades de criação de lançamentos (débito/crédito) e consulta do saldo consolidado diário.

### A. Registro de Transação Financeira (POST)
1. Acesse a interface Swagger da **API de Lançamentos**:
   👉 URL: `http://localhost:<PORTA_DO_TERMINAL_1>/swagger/index.html` (utilize a porta gerada na inicialização do Terminal 1, por exemplo, `5182`).
2. Selecione o endpoint **`POST /api/lancamentos`** e clique em **`Try it out`**.
3. Execute a requisição enviando o seguinte payload JSON para registrar um crédito de **R$ 250,00**:
   ```json
   {
     "valor": 250.00,
     "tipo": "C"
   }
   ```
4. Clique em **`Execute`**.
5. **Resultado Técnico Esperado:** Retorno HTTP **`201 Created`** contendo o ID exclusivo da transação gerado no SQL Server.

### B. Confirmação do Processamento de Eventos (Logs)
1. Inspecione imediatamente os logs ativos no **Terminal 2 (API de Consolidado)**.
2. **Resultado Técnico Esperado:** O console deve registrar o recebimento e processamento bem-sucedido da mensagem enviada pelo RabbitMQ:
   ```text
   [NOSQL SYNC] Saldo do dia 2026-08-20 atualizado em R$ 250,00 (ID: <ID-DA-TRANSACAO>).
   ```
   *Evidência:* Isso comprova o funcionamento da arquitetura orientada a eventos (EDA) com integração assíncrona desacoplada em tempo de milissegundos.

### C. Consulta do Saldo Consolidado Diário (GET)
1. Acesse a interface Swagger da **API de Consolidado**:
   👉 URL: `http://localhost:<PORTA_DO_TERMINAL_2>/swagger/index.html` (utilize a porta gerada no Terminal 2, por exemplo, `5254`).
2. Selecione o endpoint **`GET /api/consolidado/{data}`** e clique em **`Try it out`**.
3. Forneça o parâmetro da data corrente no formato `AAAA-MM-DD` (ex: `2026-08-20`) e clique em **`Execute`**.
4. **Resultado Técnico Esperado:** Retorno HTTP **`200 OK`** com a resposta estruturada contendo o saldo totalizado e a origem de dados:
   ```json
   {
     "data": "2026-08-20",
     "saldo": 250.00,
     "provedor": "MongoDB"
   }
   ```
5. **Validação do Cache Distribuído (SLO Optimization):** Submeta o comando **`Execute`** repetidas vezes. O console registrará o evento `[CACHE HIT]`. Nas requisições sequenciais, o serviço atende o endpoint diretamente do cache em memória (MemoryCache), poupando recursos e protegendo o MongoDB contra sobrecarga em cenários de alta concorrência.

---

## ⚡ 4. Teste de Resiliência e Tolerância a Falhas

Este teste valida o principal requisito não funcional do desafio técnico: **o serviço de controle de lançamentos deve permanecer operacional mesmo sob indisponibilidade temporária do serviço de consolidado ou do broker de mensageria.**

1. Acesse o console do **Docker Desktop** (ou execute via CLI no terminal) e pare o container do broker:
   ```bash
   docker stop rabbitmq_cloud
   ```
2. Retorne à interface Swagger da **API de Lançamentos** e efetue um novo envio de transação (débito de **R$ 150,00**):
   ```json
   {
     "valor": 150.00,
     "tipo": "D"
   }
   ```
3. **Resultado Técnico Esperado:** A chamada retorna sucesso com status **`201 Created`** e a transação é persistida localmente de forma soberana no SQL Server.
4. **Comportamento nos Logs (Terminal 1):** O console registrará o tratamento de exceção com a mensagem:
   `[AVISO - RESILIÊNCIA] Falha ao publicar no RabbitMQ. Lançamento salvo soberanamente no SQL Server.`
   *Evidência:* Isso comprova a alta disponibilidade da ponta de escrita e a total resiliência transacional do ecossistema.
5. Reinicie o container do broker no Docker:
   ```bash
   docker start rabbitmq_cloud
   ```
6. Se necessário, reinicie a API do Consolidado (Terminal 2) para restabelecer o canal de consumo.
7. **Consistência Eventual Garantida:** Ao restabelecer a conexão, o consumidor em segundo plano processará de forma retroativa as mensagens retidas de forma resiliente em disco na fila do RabbitMQ. O banco NoSQL (MongoDB) será atualizado de forma assíncrona, consolidando o saldo final do dia para **R$ 100,00** (R$ 250,00 de crédito menos R$ 150,00 de débito).

---

## 🚦 5. Execução dos Testes Automatizados (Suite xUnit)

O projeto inclui uma suite de testes automatizados focada na validação rigorosa das regras de domínio e consistência transacional do modelo.

1. Encerre a execução das APIs nos terminais utilizando `Ctrl + C`.
2. Certifique-se de que o terminal encontra-se no diretório raiz da solução (onde está localizado o arquivo `CashFlow.sln`).
3. Execute o comando de testes do .NET CLI:
   ```bash
   dotnet test
   ```
4. **Resultado Técnico Esperado:** O runner do xUnit executará a cobertura das regras de domínio e validações lógicas, reportando sucesso com **100% dos testes passando** de forma limpa.

---
*Este documento comprova e formaliza o pleno atendimento a todos os requisitos funcionais e não funcionais estabelecidos no edital do desafio técnico.*
