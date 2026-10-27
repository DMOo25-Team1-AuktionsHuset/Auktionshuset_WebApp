# Opsæt login og Vault-adgang

Den samlede og opdaterede vejledning findes nu i
[Kom i gang med Auktionshuset på din Windows-pc](team-setup.md).

Følg den fra trin 1, hvis du opsætter en ny pc. Hvis din personlige Vault-adgang
allerede virker, findes de lokale RabbitMQ- og login-indstillinger i trin 5.

Databasebrugernavn og adgangskode hentes nu fra Vault. Den tidligere vejlednings
`ConnectionStrings:DefaultConnection` skal derfor ikke tilføjes til User Secrets.

Appens login bruger fortsat en lokalt konfigureret bootstrap-konto og signing key,
indtil indlæsning af de resterende secrets fra Vault er implementeret.
