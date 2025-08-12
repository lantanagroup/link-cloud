/** @type {import('@eventcatalog/core/bin/eventcatalog.config').Config} */

import * as fs from 'fs';

const baseUrl = process.env.BASE_URL ? process.env.BASE_URL : '/';
const customDocsJson = fs.existsSync('./custom-docs.json') ? 
    fs.readFileSync('./custom-docs.json', 'utf8').toString() : 
    fs.readFileSync('../custom-docs.json', 'utf8').toString();
const customDocs = JSON.parse(customDocsJson);

export default {
  title: 'Link EC',
  tagline: 'This internal platform provides a comprehensive view of our event-driven architecture across all systems. Use this portal to discover existing domains, explore services and their dependencies, and understand the message contracts that connect our infrastructure',
  organizationName: 'Lantana Consulting Group',
  homepageLink: 'https://lantanagroup.github.io/link-cloud',
  editUrl: 'https://github.com/lantanagroup/link-cloud/tree/dev/docs',
  trailingSlash: false,
  // Change to make the base url of the site different, by default https://{website}.com/docs,
  // changing to /company would be https://{website}.com/company/docs,
  base: baseUrl,
  // Customize the logo, add your logo to public/ folder
  logo: {
    alt: 'EventCatalog Logo',
    src: '/logo.png',
    text: 'EventCatalog'
  },
  // Enable RSS feed for your eventcatalog
  rss: {
    enabled: true,
    // number of items to include in the feed per resource (event, service, etc)
    limit: 20
  },
	chat: {
		enabled: true,
		model: 'Hermes-3-Llama-3.2-3B-q4f16_1-MLC',
		max_tokens: 4096,
		similarityResults: 50
	},
  generators: [
		[
			"@eventcatalog/generator-ai", {
				splitMarkdownFiles: true,
				includeUsersAndTeams: false,
				includeSchemas: true,
				includeCustomDocumentation: true,
				embedding: {
					provider: 'huggingface',
					model: 'Xenova/all-MiniLM-L6-v2'
				}
			}
		],
  ],
  customDocs: customDocs,
  // required random generated id used by eventcatalog
  cId: '19993e2d-5d40-485e-8301-a445c8325f6e'
}
