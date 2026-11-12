// Run with mongosh --file papertrail-mongo.js.
db = db.getSiblingDB("papertrail");

if (db.getCollectionNames().includes("services")) {
    throw new Error("Installation stopped: services already exists. Use a new database instance.");
}

db.createCollection("services");

// Same sample data as papertrail.sql; these are not official public services.
db.services.insertMany([
    {
        _id: NumberLong("1"),
        title: "Cadastro municipal — exemplo fictício",
        description: "Exemplo acadêmico de preparação; não representa exigências de órgão público.",
        cityName: "Cidade de demonstração",
        version: NumberLong("2"),
        category: {
            id: NumberLong("1"),
            name: "Cadastro e documentação"
        },
        requirements: [
            {
                id: NumberLong("11"),
                title: "Identificação de exemplo",
                required: true,
                sourceUrl: "https://example.invalid/papertrail/1"
            },
            {
                id: NumberLong("12"),
                title: "Comprovante de exemplo",
                required: true,
                sourceUrl: "https://example.invalid/papertrail/1"
            },
            {
                id: NumberLong("13"),
                title: "Protocolo anterior de exemplo",
                required: false,
                sourceUrl: "https://example.invalid/papertrail/1"
            }
        ]
    },
    {
        _id: NumberLong("2"),
        title: "Credencial de mobilidade — exemplo fictício",
        description: "Exemplo acadêmico de checklist; não representa critérios de elegibilidade.",
        cityName: "Cidade de demonstração",
        version: NumberLong("1"),
        category: {
            id: NumberLong("2"),
            name: "Mobilidade"
        },
        requirements: [
            {
                id: NumberLong("21"),
                title: "Identificação de exemplo",
                required: true,
                sourceUrl: "https://example.invalid/papertrail/2"
            },
            {
                id: NumberLong("22"),
                title: "Formulário de exemplo",
                required: true,
                sourceUrl: "https://example.invalid/papertrail/2"
            },
            {
                id: NumberLong("23"),
                title: "Informação complementar de exemplo",
                required: false,
                sourceUrl: "https://example.invalid/papertrail/2"
            }
        ]
    },
    {
        _id: NumberLong("3"),
        title: "Atendimento cadastral — exemplo fictício",
        description: "Exemplo acadêmico para testes de preparativo vazio e itens opcionais.",
        cityName: "Cidade de demonstração",
        version: NumberLong("1"),
        category: {
            id: NumberLong("1"),
            name: "Cadastro e documentação"
        },
        requirements: [
            {
                id: NumberLong("31"),
                title: "Identificação de exemplo",
                required: true,
                sourceUrl: "https://example.invalid/papertrail/3"
            },
            {
                id: NumberLong("32"),
                title: "Anotação opcional de exemplo",
                required: false,
                sourceUrl: "https://example.invalid/papertrail/3"
            }
        ]
    }
]);

if (db.services.countDocuments({}) !== 3) {
    throw new Error("Expected 3 sample services.");
}

print("Report 1. Services by category");
db.services.find(
    {},
    { _id: 1, title: 1, "category.id": 1, "category.name": 1 }
).sort({ "category.id": 1, _id: 1 }).forEach(printjson);
