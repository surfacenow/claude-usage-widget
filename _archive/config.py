import os
from dotenv import load_dotenv

load_dotenv()

SESSION_KEY = os.getenv("SESSION_KEY", "")
