# ADR 0002: Estratégia de Dados para Integridade de Vendas e Performance de Relatórios

## Status
Aceito

## Contexto (O Problema de Negócio)
O sistema precisa atender a duas necessidades fundamentais que possuem características conflitantes. Primeiro, garantir que cada venda registrada pelo comerciante seja gravada com segurança total para auditoria financeira. Segundo, entregar o relatório de saldo diário com velocidade extrema, suportando picos de 50 consultas por segundo sem que o sistema apresente lentidão. 

Tentar utilizar apenas um banco de dados para ambas as funções poderia gerar um gargalo operacional: relatórios pesados poderiam travar o registro de novas vendas, prejudicando a receita do comerciante.

## Decisão (A Solução Estratégica)
Adotamos o modelo de Persistência Poliglota, separando as responsabilidades de armazenamento para maximizar a eficiência:

1. **SQL Server para Controle de Lançamentos:** Utilizamos um motor relacional para as transações financeiras, garantindo que nenhum dado seja perdido ou duplicado, atendendo aos critérios de conformidade e governança financeira.
2. **MongoDB para Consultas e Relatórios:** Utilizamos um banco de alta performance para o saldo consolidado. Nesta camada, os dados já ficam preparados para leitura rápida, permitindo que o comerciante visualize seu desempenho de forma instantânea.

## Consequências (Trade-offs de Negócio)
* **Experiência do Usuário:** O relatório de saldo é entregue sem atrasos, mesmo nos horários de maior movimento, aumentando a satisfação do cliente.
* **Otimização de Custos:** Esta separação permite escalar apenas a infraestrutura de relatórios durante picos de demanda, sem a necessidade de gastar com o licenciamento do banco de transações.
* **Continuidade Operacional:** Caso o banco de relatórios precise de manutenção, a operação vital de registro de vendas continua funcionando de forma independente, protegendo o fluxo de caixa.
