// Seed generator for the shop: about 1,000 products and about 20 brands.
//
// Run it from the repo root:   node tools/seed-gen/generate.js
//
// It writes two files that the API seeds from (see StoreContextSeed):
//   Infrastructure/Data/SeedData/products.json
//   Infrastructure/Data/SeedData/brands.json
//
// Rules (docs/agentic/04-database-and-seed.md §4):
//   - The original 18 products and 6 brands come first and are copied UNCHANGED
//     from original-products.json / original-brands.json next to this script.
//   - It uses a FIXED random seed, so every run gives exactly the same output.
//     Change SEED only if you really want a different catalogue.
//   - Products name their brand and type by NAME (BrandName / TypeName), so the
//     seed works no matter which ids the database gave them.
//   - No network, no API calls: descriptions come from templates.

"use strict";

const fs = require("fs");
const path = require("path");

const REPO_ROOT = path.resolve(__dirname, "..", "..");
const SEED_DATA_DIR = path.join(REPO_ROOT, "Infrastructure", "Data", "SeedData");
const IMAGES_DIR = path.join(REPO_ROOT, "API", "Content", "images", "products");

const SEED = 20261119;          // fixed random seed: same output every run
const PRODUCTS_PER_TYPE = 250;  // 4 types x 250 = 1,000 new products

// ---------------------------------------------------------------------------
// A small random number generator we can seed (Math.random cannot be seeded).
// "mulberry32": a tiny, well-known generator. Good enough for test data.
// ---------------------------------------------------------------------------
function createRandom(seed) {
  let state = seed >>> 0;
  return function next() {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296; // a number from 0 (inclusive) to 1 (exclusive)
  };
}

const random = createRandom(SEED);

function pick(list) {
  return list[Math.floor(random() * list.length)];
}

function between(min, max) {
  return min + random() * (max - min);
}

// Picks `count` different items from a list.
function pickSome(list, count) {
  const copy = list.slice();
  const result = [];
  while (result.length < count && copy.length > 0) {
    result.push(copy.splice(Math.floor(random() * copy.length), 1)[0]);
  }
  return result;
}

// ---------------------------------------------------------------------------
// Catalogue data
// ---------------------------------------------------------------------------

// Invented outdoor brand names (not real companies), added after the original 6.
const NEW_BRANDS = [
  "Summit Peak", "Frostline", "Alpine Forge", "Northridge", "Glacier Co",
  "Powder Lab", "Ridgeback", "Ice Fox", "Timberwolf", "Snowcraft",
  "Crestline", "Blizzard Works", "Polar Path", "Avalanche Gear",
];

// The 4 existing types. Ids match types.json; names are what the seed matches on.
// `prefix` is the start of the image file names for that type.
const TYPES = [
  {
    name: "Boards", noun: "Board", prefix: "sb-",
    minPrice: 120, maxPrice: 650,
    materials: ["Maple", "Bamboo", "Carbon"],
    models: ["Carver", "Freeride", "Park", "Powder", "Slasher", "Glide", "Edge", "Drift", "Rocket", "Shred"],
    features: [
      "a medium flex that forgives small mistakes",
      "a stiff flex for speed and big turns",
      "a rocker profile that floats in deep powder",
      "a camber profile with strong edge grip on hard snow",
      "a sintered base that stays fast all season",
      "a twin shape, so it rides the same both ways",
      "steel edges that hold on icy mornings",
      "a light core that is easy to carry up the hill",
    ],
    uses: [
      "Great for beginners finding their feet.",
      "Built for riders who love the terrain park.",
      "Made for all-mountain days, groomers to trees.",
      "A strong pick for fast carving on piste.",
      "Ideal for deep powder days off the groomed runs.",
    ],
  },
  {
    name: "Hats", noun: "Hat", prefix: "hat-",
    minPrice: 9, maxPrice: 60,
    materials: ["Wool", "Cotton", "Fleece", "Acrylic"],
    models: ["Beanie", "Cuff", "Pom", "Trail", "Slouch", "Watch", "Summit", "Fold", "Bobble", "Ridge"],
    features: [
      "a soft fleece band that keeps your ears warm",
      "a fold-up cuff for an extra warm layer",
      "a classic pom on top",
      "a stretchy rib knit that fits most heads",
      "a thin profile that fits under a helmet",
      "a moisture-wicking lining that stays dry",
      "a reflective stitch for dark winter evenings",
    ],
    uses: [
      "Good for city winters and the school run.",
      "Made for cold days on the slopes.",
      "Light enough for winter running.",
      "Perfect for camping trips and chilly nights.",
      "An easy everyday hat for the colder months.",
    ],
  },
  {
    name: "Boots", noun: "Boot", prefix: "boot-",
    minPrice: 60, maxPrice: 320,
    materials: ["Leather", "Synthetic", "Gore-Tex"],
    models: ["Trail", "Summit", "Storm", "Glacier", "Ranger", "Tundra", "Peak", "Drift", "Trek", "Ridge"],
    features: [
      "a waterproof membrane that keeps your feet dry",
      "an insulated lining rated for deep cold",
      "a grippy rubber sole for ice and wet rock",
      "a quick-lace system you can use with gloves on",
      "a padded collar that protects your ankles",
      "a heat-moulding liner that shapes to your foot",
      "a light build that is easy on long walks",
    ],
    uses: [
      "Built for winter hiking and snowy trails.",
      "Made for long days riding at the resort.",
      "Good for city winters and slushy streets.",
      "A strong pick for backcountry trips.",
      "Comfortable enough to wear all day.",
    ],
  },
  {
    name: "Gloves", noun: "Glove", prefix: "glove-",
    minPrice: 12, maxPrice: 110,
    materials: ["Wool", "Leather", "Fleece", "Gore-Tex"],
    models: ["Storm", "Trail", "Grip", "Summit", "Frost", "Shell", "Pipe", "Ridge", "Tour", "Arctic"],
    features: [
      "a touch-screen thumb and finger",
      "a warm fleece lining",
      "a waterproof outer layer",
      "a long cuff that seals out snow",
      "a leather palm for a strong grip",
      "a wrist leash, so you never drop them on the lift",
      "a zip pocket for a hand warmer",
    ],
    uses: [
      "Good for city winters and light hiking.",
      "Made for cold days on the slopes.",
      "Built for wet, heavy snow.",
      "Thin enough for driving and phone use.",
      "A warm choice for very cold mornings.",
    ],
  },
];

const ADJECTIVES = [
  "Storm", "Swift", "Blaze", "Frost", "Nova", "Apex", "Thunder", "Silver",
  "Shadow", "Polar", "Wild", "Echo", "Zenith", "Aurora", "Granite", "Comet",
];

const COLORS = [
  "Black", "White", "Red", "Navy", "Blue", "Green",
  "Grey", "Orange", "Yellow", "Purple", "Pink", "Brown",
];

// ---------------------------------------------------------------------------
// Helpers for each field
// ---------------------------------------------------------------------------

// The images already in the shop, grouped by type prefix (D8: reuse the 18 images).
// Sorted, so the choice is the same on every machine.
const IMAGE_FILES = fs.readdirSync(IMAGES_DIR).filter((f) => f.endsWith(".png")).sort();

// ONE place that decides the picture. Later (D8 option) this can become
// `images/products/${type.prefix}${color.toLowerCase()}.png` once there is one image per colour.
function pictureFor(type /*, color */) {
  const matches = IMAGE_FILES.filter((f) => f.startsWith(type.prefix));
  if (matches.length === 0) throw new Error("No images found for prefix " + type.prefix);
  return "images/products/" + pick(matches);
}

// A price inside the type's range that ends in .99 or .00.
function priceFor(type) {
  const whole = Math.floor(between(type.minPrice, type.maxPrice));
  return random() < 0.6 ? whole + 0.99 : whole;
}

// 0 to 120, with about 8% out of stock so "in stock" filters matter.
function stockFor() {
  if (random() < 0.08) return 0;
  return 1 + Math.floor(random() * 120);
}

// 2.5 to 5.0, skewed towards high ratings (squaring a 0-1 number pushes it towards 0).
function ratingFor() {
  const r = random();
  return Math.round((5.0 - 2.5 * r * r) * 10) / 10;
}

// A short, readable description from templates, e.g.
// "Warm wool gloves in navy, with a touch-screen thumb and a fleece lining. Good for city winters."
function descriptionFor(type, color, material) {
  const [first, second] = pickSome(type.features, 2);
  const noun = type.noun.toLowerCase() + (type.name === "Boards" ? "" : "s");
  const opening = type.name === "Boards"
    ? `A ${color.toLowerCase()} ${material.toLowerCase()} snowboard`
    : `${capitalise(material)} ${noun} in ${color.toLowerCase()}`;
  return `${opening}, with ${first} and ${second}. ${pick(type.uses)}`;
}

function capitalise(text) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

// ---------------------------------------------------------------------------
// Build the catalogue
// ---------------------------------------------------------------------------

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function main() {
  const originalBrands = readJson(path.join(__dirname, "original-brands.json"));
  const originalProducts = readJson(path.join(__dirname, "original-products.json"));
  const typeNameById = { 1: "Boards", 2: "Hats", 3: "Boots", 4: "Gloves" };
  const brandNameById = {};
  originalBrands.forEach((b) => { brandNameById[b.Id] = b.Name; });

  // Brands: the original 6 keep their ids; new ones get no id (the seed numbers them).
  const brands = originalBrands.map((b) => ({ Id: b.Id, Name: b.Name }))
    .concat(NEW_BRANDS.map((name) => ({ Name: name })));
  const brandNames = brands.map((b) => b.Name);

  // The original 18 products, unchanged, with brand and type by name.
  // Their stock and rating match what the migration sets on existing rows (25 and 4.0).
  const products = originalProducts.map((p) => ({
    Name: p.Name,
    Description: p.Description,
    Price: p.Price,
    PictureUrl: p.PictureUrl,
    TypeName: typeNameById[p.ProductTypeId],
    BrandName: brandNameById[p.ProductBrandId],
    Color: null,
    Material: null,
    StockQuantity: 25,
    Rating: 4.0,
  }));

  const usedNames = new Set(products.map((p) => p.Name.toLowerCase()));

  TYPES.forEach((type) => {
    let made = 0;
    let tries = 0;
    while (made < PRODUCTS_PER_TYPE) {
      if (++tries > PRODUCTS_PER_TYPE * 50) throw new Error("Could not make enough unique names for " + type.name);

      const brand = pick(brandNames);
      const name = `${brand} ${pick(ADJECTIVES)} ${pick(type.models)} ${type.noun}`;
      if (usedNames.has(name.toLowerCase())) continue; // names must be unique: try again
      usedNames.add(name.toLowerCase());

      const color = pick(COLORS);
      const material = pick(type.materials);
      products.push({
        Name: name,
        Description: descriptionFor(type, color, material),
        Price: priceFor(type),
        PictureUrl: pictureFor(type, color),
        TypeName: type.name,
        BrandName: brand,
        Color: color,
        Material: material,
        StockQuantity: stockFor(),
        Rating: ratingFor(),
      });
      made++;
    }
  });

  fs.writeFileSync(path.join(SEED_DATA_DIR, "brands.json"), JSON.stringify(brands, null, 2) + "\n");
  fs.writeFileSync(path.join(SEED_DATA_DIR, "products.json"), JSON.stringify(products, null, 2) + "\n");

  const outOfStock = products.filter((p) => p.StockQuantity === 0).length;
  console.log(`Wrote ${brands.length} brands and ${products.length} products ` +
    `(${originalProducts.length} original + ${products.length - originalProducts.length} new, ${outOfStock} out of stock).`);
}

main();
