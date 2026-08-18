# Estratégia de Segurança e Proteção de Ativos — Resilient Ledger

## 1. Visão Geral
Este documento descreve as camadas de segurança implementadas na arquitetura do sistema de Fluxo de Caixa. O desenho segue o princípio de Security by Design, garantindo a proteção de dados sensíveis e a integridade das transações financeiras do comerciante.

## 2. Autenticação e Autorização
Para garantir a soberania das identidades e o controle de acesso, adotamos um modelo baseado em tokens e identidade centralizada.
* Protocolo: OAuth 2.0 com OpenID Connect (OIDC).
* Provedor de Identidade (IdP): Integração com provedor corporativo Azure AD / Entra ID para autenticação de usuários e sistemas.
* Mecanismo: Uso de JSON Web Tokens (JWT) assinados para propagação de identidade stateless entre o Gateway e os microsserviços internos.

## 3. Proteção de APIs e Perímetro
O API Gateway atua como o escudo frontal da solução, protegendo os microsserviços de acessos indevidos e sobrecarga.
* Rate Limiting: Implementado no Gateway para suportar o requisito de 50 requisições por segundo, mitigando ataques de negação de serviço (DoS) e protegendo a disponibilidade para o comerciante.
* WAF (Web Application Firewall): Filtragem de tráfego para proteção contra vulnerabilidades comuns como SQL Injection e XSS.
* Validação de Contratos: Todas as requisições passam por validação de esquema no Gateway antes de serem encaminhadas aos serviços internos.

## 4. Segurança de Dados e Infraestrutura Híbrida
Conforme definido na estratégia de infraestrutura, a comunicação entre ambientes segue padrões rígidos de isolamento.
* Criptografia em Trânsito: Comunicação obrigatoriamente via HTTPS / TLS 1.2+ em todas as interações.
* Túnel Seguro (Cloud-to-Local): A integração entre as APIs na nuvem e o banco de dados transacional (SQL Server on-premises) é realizada via VPN Site-to-Site ou Direct Connect, sem exposição dos dados à internet pública.
* Criptografia em Repouso: Dados sensíveis nos bancos SQL Server e MongoDB são criptografados utilizando padrões AES-256.

## 5. Controle de Acesso Inter-Serviços
* AMQP Seguro: A comunicação com o RabbitMQ utiliza TLS e autenticação baseada em credenciais rotativas.
* Network Policies: Isolamento de rede a nível de container, permitindo que apenas o serviço de Lançamentos publique mensagens e apenas o serviço de Consolidado as consuma.

## 6. Justificativa de Governança
Esta arquitetura elimina pontos únicos de falha e garante que, mesmo sob alta carga, a integridade do saldo do comerciante permaneça protegida, priorizando a continuidade operacional e a conformidade regulatória.
