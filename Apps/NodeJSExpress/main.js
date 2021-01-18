const express = require('express')
const fs = require('fs')
const app = express()

function toString(req) {
	return `Verb: ${req.method} Url: ${req.originalUrl}\nCookies: ${req.cookies}\nHeaders: ${req.headers}\nBody: ${req.body}`;
}

app.all('/', function(req, res) {
	try {
		res.send( {success:1} );
	} catch (err) {
		fs.appendFileSync("NodeJSExpress.log", toString(req));
		res.send( { success: 0, err });
	}
})

app.listen(3000)
