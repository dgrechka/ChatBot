<|begin_of_text|><|start_header_id|>system<|end_header_id|>

Finish the text string provided by the user. Finish it by following the instructions in the user message. Your generated text will be concatenated to the user message.<|eot_id|><|start_header_id|>user<|end_header_id|>

Your task is to update the user profile with the new information present in the conversation. The user profile consists of several fields. The fields are the following:

# User Profile Fields Description

Content of the fields can contain the following (but not limited to) information:

◄user-profile-fields-description►

# Current User Profile
Latest known user profile is the following:

```json
◄user-profile-json►
```

# Conversation to extract user profile updates from

The following conversation contains the information that should be used to update the user profile:

```conversation
◄conversation-to-process►
```

# Task


Your task is to output JSON object.

## STructure 
The JSON schema is a flat (string -> string) dictionary. Consider the object as a patch to be applied to the previous user profile.
The object **may** contain the fields described before. It **must not** contain any other fields.
It **must** contain only those fields that needs to be updated with the information extracted from the conversation.
The value of the fields must be a new value of the corresponding field in a free text form (succinct).

## Rules
If the conversation does not contain any information relevant to the profile field, the field should not be present in the output JSON object.
Updated field **must** contain both the information previously stored in it and the new information extracted from the conversation (**don't loose previously accumulated information!**).
**Only** if the information is clearly wrong or 100% outdated, you should consider remove or replace it.
You **must not** duplicate the information in the fields. If the information is already present in one of the field, you should not repeat it in another.
The exception can be moving the information from one field to another, if another field is more appropriate.
Do not include "N/A", "unknown" or other similar values, just omit the data in this case.
If some field becomes too long (>1000 chars), you may aggregate the field information and replace part of it 

Now generate the JSON file with user profile update. Omit any header, just providing the JSON object.

```json
<|eot_id|><|start_header_id|>assistant<|end_header_id|>

