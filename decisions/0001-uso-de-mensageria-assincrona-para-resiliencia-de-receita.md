ADR 0001: Separação de Fluxos de Venda e Relatórios via Mensageria
Status
Aceito
Contexto (O Problema de Negócio)
O sistema atende comerciantes que dependem da disponibilidade total para registrar seus lançamentos financeiros (vendas). O edital exige que falhas no serviço de consolidado diário não impactem o serviço de lançamentos
. Além disso, prevemos picos de 50 requisições por segundo, o que poderia sobrecarregar o sistema se todos os processos fossem síncronos
.
Decisão (A Solução Estratégica)
Implementamos o RabbitMQ como um broker de mensagens assíncronas entre a API de Lançamentos e a API de Consolidado
.
Ao adotar este padrão:
Priorizamos a Receita: O registro da venda é concluído instantaneamente. O processamento do saldo (back-office) ocorre em segundo plano.
Amortecimento de Carga: A fila absorve os picos de tráfego, protegendo o sistema de quedas durante horários de maior movimento.
Consequências (Trade-offs de Negócio)
Ponto Positivo (Continuidade Operacional): Se o serviço de relatórios falhar, o comerciante continua vendendo normalmente. As mensagens ficam seguras na fila para processamento posterior.
Risco Aceito (Consistência Eventual): O saldo do comerciante pode levar alguns segundos para ser atualizado após uma venda. Entendemos que para o negócio, é preferível um saldo brevemente desatualizado do que uma venda perdida por indisponibilidade
