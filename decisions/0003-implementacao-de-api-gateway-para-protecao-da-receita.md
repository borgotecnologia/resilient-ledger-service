# ADR 0003: Camada Centralizada de Proteção da Receita e Governança de Acesso

## Status
Aceito

## Contexto (O Problema de Negócio)
O sistema gerencia ativos financeiros críticos e deve suportar picos de 50 requisições por segundo, conforme as metas de disponibilidade estabelecidas. Expor as interfaces internas diretamente ao público aumentaria a superfície de ataque e fragmentaria a gestão de segurança. Além disso, picos inesperados de tráfego sem controle centralizado poderiam paralisar o serviço de lançamentos, resultando em perda imediata de faturamento para os comerciantes.

## Decisão (A Solução Estratégica)
Implementamos um API Gateway como porta de entrada única para todas as comunicações externas. Esta escolha atua como uma barreira de governança que assegura três pilares:

1. Blindagem de Ativos: Validação centralizada de identidade para garantir que apenas transações legítimas alcancem os serviços core.
2. Continuidade Operacional: Aplicação de limites de taxa para impedir que acessos excessivos degradem a experiência de uso ou causem indisponibilidade.
3. Abstração e Escalabilidade: Permite que a tecnologia interna evolua com agilidade sem exigir alterações nos sistemas dos clientes, protegendo o tempo de comercialização.

## Consequências e Justificativa de Investimento
* Eficiência em Custos de Engenharia: Ao centralizar a segurança e o controle de tráfego no Gateway, eliminamos a necessidade de replicar essas lógicas em cada nova API desenvolvida. Isso reduz o tempo e o custo de manutenção de todo o ecossistema tecnológico.
* Seguro Operacional: O custo da infraestrutura do Gateway é ínfimo se comparado ao prejuízo financeiro e de reputação gerado por uma queda total do sistema. O investimento é justificado como uma estratégia de mitigação de risco financeiro direto.
* Escalabilidade Previsível: A solução permite escalar os recursos de entrada separadamente do processamento de dados, otimizando o gasto com nuvem e garantindo que paguemos apenas pelo tráfego efetivamente gerenciado.
