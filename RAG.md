# Local knowledge base

The chat service retrieves relevant passages from local PDF (`.pdf`), Markdown (`.md`), and plain text (`.txt`) files in `KnowledgeBase/` and adds the best matches to the model context. PDFs are read and text is extracted by the app on the first search; no manual text conversion is needed. Restart the app after adding or changing source material.

Add trusted, non-secret material such as FAQs, household notes, or reference guides. Keep confidential bank, customer, credential, and account data out of this folder. The assistant is instructed to treat retrieved passages as reference data and to say when they do not answer a question.

Retrieval currently uses word overlap, so it works best when a question shares terms with the source text. Keep documents focused and include the terms people are likely to ask about. The app indexes up to four matching passages per question.
