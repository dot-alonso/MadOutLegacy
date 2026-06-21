const fs = require('fs');
const path = require('path');
const inputFile = path.join(__dirname, 'Online_SpawnPoses_AfterDead.bytes');
const outputFile = path.join(__dirname, 'RespawnPoints.json');
try {
    if (!fs.existsSync(inputFile)) {
        console.error(`\x1b[31m[ERROR] Input file not found at: ${inputFile}\x1b[0m`);
        process.exit(1);
    }

    const buffer = fs.readFileSync(inputFile);
    const totalPoints = buffer.readInt32LE(0);
    console.log(`\x1b[36m[INFO] Found ${totalPoints} spawn positions in binary file.\x1b[0m`)
    let pointsArray = [];
    let offset = 4; 
    for (let i = 0; i < totalPoints; i++) {
        if (offset + 12 > buffer.length) {
            console.log(`\x1b[33m[WARNING] Byte stream truncated at index #${i}\x1b[0m`);
            break;
        }

        const x = buffer.readFloatLE(offset);
        const y = buffer.readFloatLE(offset + 4);
        const z = buffer.readFloatLE(offset + 8);
        pointsArray.push({
            x: parseFloat(x.toFixed(2)),
            y: parseFloat(y.toFixed(2)),
            z: parseFloat(z.toFixed(2)),
            id: i + 1
        });

        offset += 12;
    }

    fs.writeFileSync(outputFile, JSON.stringify(pointsArray, null, 4), 'utf-8');
    console.log(`\x1b[32m[SUCCESS] JSON config successfully generated: ${outputFile}\x1b[0m`);
} catch (error) {
    console.error(`\x1b[31m[CRASH] Script failed with error: ${error.message}\x1b[0m`);
}