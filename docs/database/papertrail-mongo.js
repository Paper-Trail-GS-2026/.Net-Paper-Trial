// Run with mongosh --file papertrail-mongo.js.
db = db.getSiblingDB("papertrail");

const existing = db.getCollectionNames();
if (existing.includes("services") || existing.includes("preparations")) {
    throw new Error("Installation stopped: PaperTrail collections already exist. Use an empty database.");
}

db.createCollection("services", {
    validator: { $jsonSchema: {
        bsonType: "object",
        required: ["_id", "title", "description", "cityName", "version", "category", "requirements"],
        properties: {
            _id: { bsonType: "long", minimum: 1 },
            title: { bsonType: "string", minLength: 1, maxLength: 160 },
            description: { bsonType: "string" },
            cityName: { bsonType: "string" },
            version: { bsonType: "long", minimum: 1 },
            category: { bsonType: "object", required: ["id", "name"], properties: {
                id: { bsonType: "long", minimum: 1 }, name: { bsonType: "string", minLength: 1 }
            } },
            requirements: { bsonType: "array", items: {
                bsonType: "object", required: ["id", "title", "required", "sourceUrl"], properties: {
                    id: { bsonType: "long", minimum: 1 }, title: { bsonType: "string", minLength: 1 },
                    required: { bsonType: "bool" }, sourceUrl: { bsonType: "string" }
                }
            } }
        }
    } }
});

db.createCollection("preparations", {
    validator: { $jsonSchema: {
        bsonType: "object",
        required: ["_id", "ownerSubject", "serviceId", "sourceVersion", "title", "createdAt", "items"],
        properties: {
            _id: { bsonType: "long", minimum: 1 },
            ownerSubject: { bsonType: "string", minLength: 1, maxLength: 128 },
            serviceId: { bsonType: "long", minimum: 1 },
            sourceVersion: { bsonType: "long", minimum: 1 },
            title: { bsonType: "string", minLength: 1, maxLength: 160 },
            createdAt: { bsonType: "date" },
            items: { bsonType: "array", items: {
                bsonType: "object", required: ["id", "requirementId", "title", "required", "completed"], properties: {
                    id: { bsonType: "long", minimum: 1 },
                    requirementId: { bsonType: ["long", "null"] },
                    title: { bsonType: "string", minLength: 1, maxLength: 200 },
                    required: { bsonType: "bool" }, completed: { bsonType: "bool" }
                }
            } }
        }
    } }
});

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

db.preparations.insertMany([
  {
    "_id": NumberLong("101"),
    "ownerSubject": "fixture-citizen-a",
    "serviceId": NumberLong("1"),
    "sourceVersion": NumberLong("1"),
    "title": "Cadastro pendente",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("101"),
        "requirementId": NumberLong("11"),
        "title": "Identificação de exemplo",
        "required": true,
        "completed": false
      },
      {
        "id": NumberLong("102"),
        "requirementId": NumberLong("12"),
        "title": "Comprovante de exemplo",
        "required": true,
        "completed": false
      },
      {
        "id": NumberLong("103"),
        "requirementId": NumberLong("13"),
        "title": "Protocolo anterior de exemplo",
        "required": false,
        "completed": true
      }
    ]
  },
  {
    "_id": NumberLong("102"),
    "ownerSubject": "fixture-citizen-a",
    "serviceId": NumberLong("1"),
    "sourceVersion": NumberLong("1"),
    "title": "Cadastro parcial",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("104"),
        "requirementId": NumberLong("11"),
        "title": "Identificação de exemplo",
        "required": true,
        "completed": true
      },
      {
        "id": NumberLong("105"),
        "requirementId": NumberLong("12"),
        "title": "Comprovante de exemplo",
        "required": true,
        "completed": false
      },
      {
        "id": NumberLong("106"),
        "requirementId": NumberLong("13"),
        "title": "Protocolo anterior de exemplo",
        "required": false,
        "completed": false
      }
    ]
  },
  {
    "_id": NumberLong("103"),
    "ownerSubject": "fixture-citizen-b",
    "serviceId": NumberLong("1"),
    "sourceVersion": NumberLong("2"),
    "title": "Cadastro completo com opcional pendente",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("107"),
        "requirementId": NumberLong("11"),
        "title": "Identificação de exemplo",
        "required": true,
        "completed": true
      },
      {
        "id": NumberLong("108"),
        "requirementId": NumberLong("12"),
        "title": "Comprovante de exemplo",
        "required": true,
        "completed": true
      },
      {
        "id": NumberLong("109"),
        "requirementId": NumberLong("13"),
        "title": "Protocolo anterior de exemplo",
        "required": false,
        "completed": false
      }
    ]
  },
  {
    "_id": NumberLong("104"),
    "ownerSubject": "fixture-citizen-a",
    "serviceId": NumberLong("2"),
    "sourceVersion": NumberLong("1"),
    "title": "Credencial pendente",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("110"),
        "requirementId": NumberLong("21"),
        "title": "Identificação de exemplo",
        "required": true,
        "completed": false
      },
      {
        "id": NumberLong("111"),
        "requirementId": NumberLong("22"),
        "title": "Formulário de exemplo",
        "required": true,
        "completed": false
      }
    ]
  },
  {
    "_id": NumberLong("105"),
    "ownerSubject": "fixture-citizen-b",
    "serviceId": NumberLong("2"),
    "sourceVersion": NumberLong("1"),
    "title": "Credencial completa",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("112"),
        "requirementId": NumberLong("21"),
        "title": "Identificação de exemplo",
        "required": true,
        "completed": true
      },
      {
        "id": NumberLong("113"),
        "requirementId": NumberLong("22"),
        "title": "Formulário de exemplo",
        "required": true,
        "completed": true
      }
    ]
  },
  {
    "_id": NumberLong("106"),
    "ownerSubject": "fixture-citizen-b",
    "serviceId": NumberLong("3"),
    "sourceVersion": NumberLong("1"),
    "title": "Somente item opcional",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [
      {
        "id": NumberLong("114"),
        "requirementId": NumberLong("32"),
        "title": "Anotação opcional de exemplo",
        "required": false,
        "completed": true
      }
    ]
  },
  {
    "_id": NumberLong("107"),
    "ownerSubject": "fixture-citizen-a",
    "serviceId": NumberLong("3"),
    "sourceVersion": NumberLong("1"),
    "title": "Preparativo vazio",
    "createdAt": ISODate("2026-11-09T12:00:00+00:00"),
    "items": [

    ]
  }
]);

if (db.services.countDocuments({}) !== 3 || db.preparations.countDocuments({}) !== 7) {
    throw new Error("Expected three services and seven preparations.");
}

// Report 1: category data is embedded in each service.
const report1 = db.services.find({}, { _id: 1, title: 1, category: 1 })
    .sort({ "category.id": 1, _id: 1 }).toArray().map(service => ({
        categoryId: service.category.id, categoryName: service.category.name,
        serviceId: service._id, title: service.title
    }));

// Report 2: required and pending must match the same item.
const report2 = db.preparations.find(
    { items: { $elemMatch: { required: true, completed: false } } },
    { _id: 1, title: 1, ownerSubject: 1 }
).sort({ _id: 1 }).toArray().map(preparation => ({
    preparationId: preparation._id, title: preparation.title, ownerSubject: preparation.ownerSubject
}));

// Report 3: copied items decide progress; the catalog supplies the current version.
const report3 = db.preparations.aggregate([
    { $set: { requiredItems: { $filter: { input: "$items", as: "item", cond: { $eq: ["$$item.required", true] } } } } },
    { $set: {
        requiredCount: { $size: "$requiredItems" },
        pendingCount: { $size: { $filter: { input: "$requiredItems", as: "item", cond: { $eq: ["$$item.completed", false] } } } }
    } },
    { $lookup: { from: "services", localField: "serviceId", foreignField: "_id", as: "currentService" } },
    { $set: { currentVersion: { $ifNull: [{ $arrayElemAt: ["$currentService.version", 0] }, null] } } },
    { $project: {
        _id: 0, preparationId: "$_id", title: 1, totalItems: { $size: "$items" },
        requiredItems: "$requiredCount", pendingRequiredItems: "$pendingCount",
        completionPercent: { $cond: [
            { $gt: ["$requiredCount", 0] },
            { $round: [{ $multiply: [{ $divide: [{ $subtract: ["$requiredCount", "$pendingCount"] }, "$requiredCount"] }, 100] }, 2] },
            0
        ] },
        ready: { $and: [{ $gt: ["$requiredCount", 0] }, { $eq: ["$pendingCount", 0] }] },
        sourceOutdated: { $gt: [{ $ifNull: ["$currentVersion", "$sourceVersion"] }, "$sourceVersion"] },
        sourceVersion: 1, currentVersion: 1
    } },
    { $sort: { preparationId: 1 } }
]).toArray();

const expected = [
    [101, 3, 2, 2, 0, false, true], [102, 3, 2, 1, 50, false, true],
    [103, 3, 2, 0, 100, true, false], [104, 2, 2, 2, 0, false, false],
    [105, 2, 2, 0, 100, true, false], [106, 1, 0, 0, 0, false, false],
    [107, 0, 0, 0, 0, false, false]
];
if (report1.length !== 3 || report2.map(row => row.preparationId.toString()).join(",") !== "101,102,104"
    || report3.length !== expected.length) throw new Error("Unexpected report rows.");
report3.forEach((row, index) => {
    const actual = [Number(row.preparationId), row.totalItems, row.requiredItems,
        row.pendingRequiredItems, row.completionPercent, row.ready, row.sourceOutdated];
    if (JSON.stringify(actual) !== JSON.stringify(expected[index])) {
        throw new Error("Unexpected progress for preparation " + row.preparationId);
    }
});

print("REPORT_1_JSON=" + EJSON.stringify(report1, { relaxed: true }));
print("REPORT_2_JSON=" + EJSON.stringify(report2, { relaxed: true }));
print("REPORT_3_JSON=" + EJSON.stringify(report3, { relaxed: true }));
print("MongoDB: two collections, ten documents and three reports OK.");
