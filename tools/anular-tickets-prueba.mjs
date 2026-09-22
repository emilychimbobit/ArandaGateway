// Anula tickets de prueba directo contra Aranda.
//
// Existe porque la anulacion no tiene camino por APIM: el spec publica solo
// "get" en /api/v9/item/{id} (docs/iac/apim/API-FC-INT-GestionAranda.json), asi
// que el gateway responde ARANDA_404 al intentar cancelar. Mientras eso siga
// asi, cada prueba de creacion deja un ticket que hay que cerrar por fuera.
//
// Node y no curl, PowerShell ni HttpClient de .NET: esos tres reciben el
// Managed Challenge de Cloudflare en el 100% de los intentos desde una maquina
// de desarrollo. Node comparte fingerprint TLS con PostmanRuntime, que es el
// unico cliente que pasa. Aun asi el desafio es intermitente, de ahi el
// reintento.
//
// El token de X-Authorization alcanzaba hasta el 16 de septiembre de 2026. El
// 18 el origen empezo a responder 401 ("You do not have permission to view this
// directory or page.", de IIS) a toda peticion de /ASMSAPI sin AuthCookieASMS,
// directo y por APIM, con o sin token. Con una cookie viva vuelve a responder,
// asi que ARANDA_COOKIE es opcional pero hoy hace falta.
//
// Uso:
//   $env:ARANDA_DIRECT_URL = 'https://mesadeayuda.divisionminera.com/ASMSAPI'
//   $env:ARANDA_TOKEN      = 'Bearer <token>'
//   $env:ARANDA_COOKIE     = 'AuthCookieASMS=<cookie>'   # opcional
//   node tools/anular-tickets-prueba.mjs RF-59557 RF-59468
const baseUrl = process.env.ARANDA_DIRECT_URL?.replace(/\/$/, '');
const token = process.env.ARANDA_TOKEN;
const cookie = process.env.ARANDA_COOKIE;

if (!baseUrl || !token) {
  console.error('Faltan ARANDA_DIRECT_URL o ARANDA_TOKEN.');
  process.exit(1);
}

const COMMENTARY =
  'Cancelación de ticket generado durante prueba controlada de integración.';

// Estado 61 = Cancelado en el modelo 17.
const CANCELLED_STATE_ID = 61;
const MAX_ATTEMPTS = 6;

async function request(method, path, body) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const response = await fetch(`${baseUrl}/${path}`, {
      method,
      headers: {
        'X-Authorization': token,
        'Content-Type': 'application/json',
        'User-Agent': 'PostmanRuntime/7.43.0',
        Accept: '*/*',
        ...(cookie ? { Cookie: cookie } : {})
      },
      body: body ? JSON.stringify(body) : undefined
    });

    const text = await response.text();

    if (!(response.status === 403 && /challenge/i.test(text))) {
      return { status: response.status, text };
    }

    console.log(`    (403 de Cloudflare, intento ${attempt} de ${MAX_ATTEMPTS})`);
    await new Promise(resolve => setTimeout(resolve, 3000 * attempt));
  }

  return { status: 403, text: 'Cloudflare desafio todos los intentos' };
}

const cases = process.argv.slice(2);
if (cases.length === 0) {
  console.error('uso: node tools/anular-tickets-prueba.mjs RF-59557 RF-59468 ...');
  process.exit(1);
}

for (const caseNumber of cases) {
  console.log(`=== ${caseNumber} ===`);

  const id = caseNumber.split('-').pop();
  if (!/^\d+$/.test(id)) {
    console.log(`  no pude derivar el id interno de '${caseNumber}'; salteado`);
    continue;
  }

  const read = await request('GET', `api/v9/item/${id}`);
  if (read.status !== 200) {
    console.log(`  read -> ${read.status}: ${read.text.slice(0, 200)}`);
    continue;
  }

  const ticket = JSON.parse(read.text);

  if (ticket.idByProject !== caseNumber) {
    console.log(`  el id ${id} corresponde a ${ticket.idByProject}, no a ${caseNumber}; salteado`);
    continue;
  }

  console.log(`  estado actual: ${ticket.stateName} (itemVersion ${ticket.itemVersion})`);

  if (ticket.stateName === 'Cancelado') {
    console.log('  ya estaba cancelado; nada que hacer');
    continue;
  }

  // Los valores salen del propio ticket, no del catalogo: con un itemVersion
  // que no es el vigente Aranda rechaza la actualizacion.
  const put = await request('PUT', `api/v9/item/${id}`, {
    categoryId: ticket.categoryId,
    commentary: COMMENTARY,
    consoleType: 'specialist',
    registryTypeId: ticket.registryTypeId,
    unitId: 0,
    itemType: ticket.itemType,
    itemVersion: ticket.itemVersion,
    listAdditionalField: [],
    modelId: ticket.modelId,
    projectId: ticket.projectId,
    serviceId: ticket.serviceId,
    stateId: CANCELLED_STATE_ID
  });

  console.log(`  cancel -> ${put.status}: ${put.text.slice(0, 200)}`);

  // Un 200 no alcanza como prueba: se confirma leyendo el estado de vuelta.
  if (put.status === 200) {
    const verify = await request('GET', `api/v9/item/${id}`);
    if (verify.status === 200) {
      console.log(`  verificado: estado ahora = ${JSON.parse(verify.text).stateName}`);
    }
  }
}
