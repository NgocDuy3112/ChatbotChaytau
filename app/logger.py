import logging
import sys
from logging.handlers import TimedRotatingFileHandler
from datetime import datetime
import os


# Include a timestamp in the filename to prevent overwriting logs across runs
LOG_TIMESTAMP_FORMAT = "%Y%m%d_%H%M%S"
LOG_FILE_NAME = f"chatbotchaytau_{datetime.now().strftime(LOG_TIMESTAMP_FORMAT)}.log"
LOG_FORMAT = "%(asctime)s - %(name)s - %(levelname)s - %(message)s (%(filename)s:%(lineno)d)"


def setup_logger(name: str = "chatbotchaytau_logger", level: int = logging.INFO):
    """
    Sets up a robust logger with a file handler and a console handler.
    :param name: The name of the logger to be used by all modules.
    :param level: The minimum logging level to process.
    :return: The configured logger instance.
    """
    
    # 1. Create the logger
    logger = logging.getLogger(name)
    logger.setLevel(level)

    # Prevent handlers from being added multiple times if the function is called repeatedly
    if not logger.handlers:
        # 2. Create Formatter
        formatter = logging.Formatter(LOG_FORMAT)

        # 3. Create Handlers

        # Console Handler (for real-time viewing/debugging)
        console_handler = logging.StreamHandler(sys.stdout)
        console_handler.setFormatter(formatter)
        logger.addHandler(console_handler)

        # File Handler (for persistence and log rotation)
        log_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'logs')
        os.makedirs(log_dir, exist_ok=True)
        log_path = os.path.join(log_dir, LOG_FILE_NAME)
        
        # TimedRotatingFileHandler rotates the log file daily
        file_handler = TimedRotatingFileHandler(
            log_path,
            when="midnight",
            interval=1,
            backupCount=7, # Keep 7 days of logs
            encoding='utf-8'
        )
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)

    return logger


# Create the global logger instance
global_logger = setup_logger()