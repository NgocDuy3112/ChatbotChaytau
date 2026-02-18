from google import genai

from ..configs import get_settings
from ..logger import global_logger


settings = get_settings()



def get_gemini_client() -> genai.Client:
    try:
        client = genai.Client(api_key=settings.GEMINI_API_KEY)
        return client
    except Exception as e:
        global_logger.error(f"Failed to create Gemini client: {e}")
        raise e