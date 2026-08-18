# Estratégia de Operação e Resiliência — Resilient Ledger

## 1. Visão Geral de Operação
Este documento define as diretrizes operacionais para garantir a estabilidade e a continuidade do sistema de Fluxo de Caixa. A operação é baseada em uma arquitetura híbrida que separa a carga de processamento na nuvem da persistência soberana no ambiente local, mitigando riscos de indisponibilidade total.

## 2. Observabilidade (Monitoramento e Logs)
Para garantir a visibilidade exigida no desafio técnico, adotamos três camadas de observabilidade:
* **Logs Estruturados**: As APIs em .NET 8 geram logs em formato JSON, permitindo a correlação de transações ponta a ponta desde o API Gateway até os consumidores de mensagens no consolidado.
* **Métricas de Performance**: Monitoramento em tempo real do throughput para assegurar o suporte a 50 req/s, com alertas automáticos caso a taxa de erro ou perda de requisições ultrapasse 5%.
* **Health Checks**: Endpoints de saúde (/health) implementados em todos os containers para verificar a conectividade com SQL Server, MongoDB e RabbitMQ, permitindo que o orquestrador reinicie instâncias degradadas automaticamente.

## 3. Estratégia de Deploy e Escalabilidade
A solução utiliza containerização para garantir a portabilidade e a escala elástica necessária para o negócio:
* **Escalabilidade Horizontal**: As APIs na nuvem estão configuradas para escalonamento automático (Auto-scaling) baseado no uso de CPU e memória, absorvendo picos de vendas sem degradação do serviço.
* **Isolamento de Carga**: O serviço de lançamentos (escrita) é escalado independentemente do serviço de consolidado (leitura/relatórios), otimizando o custo operacional e os recursos de infraestrutura.

## 4. Análise de Resiliência e Mitigação de SPOF
A arquitetura elimina Pontos Únicos de Falha (SPOF) através do desacoplamento assíncrono:
* **Resiliência de Receita**: O uso do RabbitMQ garante que, se o serviço de relatórios falhar, o comerciante continue registrando suas vendas normalmente. As mensagens ficam retidas de forma segura no broker até a recuperação do consumidor.
* **Tratamento de Mensagens Mortas (DLQ)**: Falhas no processamento do consolidado encaminham os eventos para Dead Letter Queues, evitando a perda de dados e permitindo o reprocessamento após correções técnicas.
* **Tolerância a Falhas de Rede**: Em caso de queda na VPN entre Nuvem e Local, as APIs na nuvem continuam recebendo e enfileirando dados, sincronizando com o SQL Server on-premises assim que a conectividade é restabelecida.

## 5. Recuperação de Falhas e Continuidade
* **Garantia ACID**: O núcleo transacional reside no SQL Server local, protegendo a integridade dos dados financeiros contra falhas de lógica ou infraestrutura.
* **Estratégia de Recuperação**: Implementação de retries exponenciais e circuit breakers no API Gateway para evitar o efeito cascata em caso de lentidão em sistemas externos ou no ERP legado.
