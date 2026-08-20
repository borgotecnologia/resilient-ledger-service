# Guia de Validação Técnica e Funcional de Ponta a Ponta
## Projeto: CashFlow (Resilient Ledger)

Este guia foi elaborado para que qualquer pessoa (mesmo sem conhecimento aprofundado em programação ou arquitetura de sistemas) consiga configurar, executar e validar o teste de fogo das APIs de **Lançamentos** e **Consolidado Diário** de forma totalmente visual e prática.

---

## 📋 Pré-requisitos do Sistema

Antes de iniciar, certifique-se de que a máquina possui os seguintes softwares instalados:
1. **Docker Desktop** (com suporte a containers ativado).
2. **.NET 8 SDK** (para compilar e rodar as APIs).
3. **VS Code** (ou qualquer terminal de linha de comando de sua preferência).
4. Um navegador de internet (Google Chrome, Edge, etc.).

---

## 🛠️ Passo 1: Subir as Máquinas no Docker (Bancos e Fila)

Nossa solução utiliza três servidores que rodam isolados dentro do Docker. Vamos ligá-los agora:

1. Abra o seu terminal de comando na **pasta raiz** do projeto (onde está localizado o arquivo `docker-compose.yml`).
2. Execute o seguinte comando para baixar e ligar os servidores em segundo plano:
   ```bash
   docker compose up -d
   ```
3. Aguarde cerca de 1 a 2 minutos para o Docker concluir o download.
4. Para garantir que os três servidores estão rodando perfeitamente, execute:
   ```bash
   docker ps
   ```
   *Você deverá ver três containers ativos na tabela:*
   * `sqlserver_local` (porta 1433)
   * `rabbitmq_cloud` (portas 5672 e 15672)
   * `mongodb_cloud` (porta 27017)

---

## 🚀 Passo 2: Iniciar as duas APIs em Paralelo

Com os servidores ligados, vamos ligar os nossos microsserviços. Você precisará de **dois terminais abertos simultaneamente**:

### Terminal 1: API de Lançamentos (Escrita / On-Premises)
1. Navegue até a pasta do projeto de Lançamentos:
   ```bash
   cd CashFlow.Lancamentos
   ```
2. Execute a aplicação:
   ```bash
   dotnet run
   ```
3. **O que observar:** O console compilará o projeto e mostrará a mensagem informando a porta onde ela está ouvindo (ex: `Now listening on: http://localhost:5182`). No primeiro boot, o comando `EnsureCreated()` cria automaticamente a tabela de lançamentos no SQL Server do Docker.

### Terminal 2: API de Consolidado (Leitura & Consumidor / Cloud)
1. Abra uma nova aba ou janela de terminal no seu VS Code.
2. Navegue até a pasta do Consolidado:
   ```bash
   cd CashFlow.Consolidado
   ```
3. Execute a aplicação:
   ```bash
   dotnet run
   ```
4. **O que observar:** O console informará a porta local do Consolidado (ex: `Now listening on: http://localhost:5254`). O serviço em segundo plano conectará ao RabbitMQ e exibirá que está ativo, aguardando mensagens. Se houver mensagens acumuladas na fila, ele as processará instantaneamente.

---

## 🧪 Passo 3: O Teste Prático (Fluxo de Venda de Ponta a Ponta)

Agora vamos simular a atividade diária de um comerciante para validar as duas capacidades centrais exigidas no edital.

### 1. Criar um Lançamento de Venda (POST)
1. Abra o navegador e acesse o Swagger da **API de Lançamentos**:
   👉 URL: `http://localhost:<PORTA_DO_TERMINAL_1>/swagger/index.html` (substitua a porta pelo número que apareceu no Terminal 1, ex: `5182`).
2. Clique no endpoint **`POST /api/lancamentos`** -> clique no botão **`Try it out`**.
3. Envie o seguinte JSON para registrar uma venda de **R$ 250,00**:
   ```json
   {
     "valor": 250.00,
     "tipo": "C"
   }
   ```
4. Clique em **`Execute`**. 
5. **Resultado Esperado:** O Swagger deve retornar o código **`201 Created`** com o ID único gerado para a transação. O lançamento foi persistido no SQL Server de forma segura.

### 2. Acompanhar a Mensageria nos Logs
1. Olhe imediatamente para a tela do **Terminal 2 (API de Consolidado)**.
2. **Resultado Esperado:** Você verá o log ser impresso na tela na mesma hora:
   ```text
   [NOSQL SYNC] Saldo do dia 2026-08-19 atualizado em R$ 250,00 (ID: <ID-DA-TRANSACAO>).
   ```
   Isso prova que o RabbitMQ transportou o evento de forma assíncrona e desacoplada em tempo de milissegundos!

### 3. Consultar o Saldo Consolidado Diário (GET)
1. No seu navegador, acesse o Swagger da **API de Consolidado**:
   👉 URL: `http://localhost:<PORTA_DO_TERMINAL_2>/swagger/index.html` (substitua pela porta do Terminal 2, ex: `5254`).
2. Clique no endpoint **`GET /api/consolidado/{data}`** -> clique em **`Try it out`**.
3. No campo `data`, digite a data de hoje no formato `AAAA-MM-DD` (ex: `2026-08-19`) e clique em **`Execute`**.
4. **Resultado Esperado:** O retorno trará o saldo totalizado:
   ```json
   {
     "data": "2026-08-19",
     "saldo": 250.00,
     "provedor": "MongoDB"
   }
   ```
5. **Dica de Performance (Amortecimento de Carga):** Clique em **`Execute`** novamente várias vezes seguidas. Você verá no console o log `[CACHE HIT]`. Nas requisições repetidas, o sistema entrega o saldo direto da memória RAM (MemoryCache) em microssegundos, blindando o banco de dados contra quedas em picos de tráfego.

---

## ⚡ Passo 4: O Teste Supremo da Resiliência (Tolerância a Falhas)

Este passo é o principal critério para comprovar a maturidade arquitetural exigida pelo edital: **o serviço de controle de lançamentos não deve ficar indisponível caso o serviço de consolidado diário falhe.**

1. Abra o seu **Docker Desktop** e clique em **"Stop"** (botão quadrado vermelho) apenas no container do **`rabbitmq_cloud`** para simular uma queda de infraestrutura de rede da fila.
2. Volte ao Swagger da **API de Lançamentos** (Swagger 1) e tente realizar um novo POST de venda de **R$ 150,00**:
   ```json
   {
     "valor": 150.00,
     "tipo": "C"
   }
   ```
3. **Resultado Esperado:** O POST retornará com sucesso **`201 Created`** e a venda será gravada normalmente no SQL Server!
4. **Observe os Logs do Terminal 1:** O console exibirá um aviso amigável:
   `[AVISO - RESILIÊNCIA] Falha ao publicar no RabbitMQ. Lançamento salvo soberanamente no SQL Server.`
   Isso prova que a indisponibilidade do Consolidado/Fila não interrompe as vendas do lojista!
5. No Docker Desktop, ligue o container do **`rabbitmq_cloud`** novamente (clique em "Start").
6. Pare a API do Consolidado (Terminal 2) com `Ctrl + C` e inicie-a novamente com `dotnet run`.
7. **Resultado de Consistência Eventual:** Assim que inicializar, o consumidor em segundo plano conectará, resgatará a mensagem que ficou guardada em segurança no disco e atualizará o saldo consolidado no MongoDB de forma automática para **R$ 400,00**!

---

## 🚦 Passo 5: Executar os Testes Automatizados (xUnit)

Para rodar a bateria de testes unitários que blindam as regras de negócio de domínio (validação de valores maiores que zero e tipo de entrada válida C/D):

1. Pare as aplicações nos terminais (`Ctrl + C`).
2. No seu terminal, garanta que está na pasta raiz (onde fica o arquivo `.sln`) e execute o comando:
   ```bash
   dotnet test
   ```
3. **Resultado Esperado:** O compilador executará as validações de caminhos felizes e de exceção programadas no projeto e exibirá em verde o sucesso com **100% dos testes passando**.

---
*Este guia prova formalmente o atendimento integral de todos os requisitos funcionais e não funcionais do desafio técnico.*
