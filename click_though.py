import sys
from PyQt5.QtCore import Qt
from PyQt5.QtGui import QColor, QPalette
from PyQt5.QtWidgets import QApplication, QMainWindow, QWidget, QLabel

class ClickThroughWindow(QMainWindow):
    def __init__(self):
        super().__init__()

        # Set the window to be transparent
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint)
        self.setAttribute(Qt.WA_TranslucentBackground)

        # Create a central widget to handle mouse events
        central_widget = QWidget(self)
        self.setCentralWidget(central_widget)

        # Set the background color to fully transparent
        palette = QPalette()
        palette.setColor(QPalette.Background, QColor(0, 0, 0, 0))
        central_widget.setPalette(palette)

        # Add a QLabel with some text
        label = QLabel("Click through this window", central_widget)
        label.setAlignment(Qt.AlignCenter)
        label.setGeometry(0, 0, 400, 300)

    def mousePressEvent(self, event):
        # Pass mouse events through to the underlying windows
        event.ignore()

def main():
    app = QApplication(sys.argv)
    window = ClickThroughWindow()
    window.setGeometry(100, 100, 400, 300)
    window.show()
    sys.exit(app.exec_())

if __name__ == '__main__':
    main()