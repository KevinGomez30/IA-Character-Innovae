require('dotenv').config(); // Esto importa las variables del .env
const WebSocket = require('ws');
const fs = require('fs');
const axios = require('axios');
const FormData = require('form-data');

const server = new WebSocket.Server({ port: 8080 });
console.log("WebSocket server running on ws://localhost:8080");

server.on('connection', ws => {
    console.log("New client connected");

    ws.on('message', async message => {
        console.log("Received audio data");

        const audioPath = "audio.opus";
        fs.writeFileSync(audioPath, message);

        try {
            const transcription = await sendToWhisper(audioPath);
            console.log("Transcription: ", transcription);
            ws.send(JSON.stringify({ transcription }));
        } catch (err) {
            console.error("Error transcribing audio: ", err);
            ws.send(JSON.stringify({ error: "Failed to transcribe audio." }));
        }
    });

    ws.on('close', () => console.log("Client disconnected"));
});

async function sendToWhisper(filePath) {
    const apiKey = process.env.OPENAI_API_KEY; // Ahora se lee desde .env
    const url = "https://api.openai.com/v1/audio/transcriptions";

    const formData = new FormData();
    formData.append('file', fs.createReadStream(filePath));
    formData.append('model', 'whisper-1');
    formData.append('language', 'es');

    const response = await axios.post(url, formData, {
        headers: {
            'Authorization': `Bearer ${apiKey}`,
            ...formData.getHeaders()
        }
    });

    return response.data.text;
}
