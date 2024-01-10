import sys, json, os
import typing
from PyQt5.QtWidgets import QApplication, QMainWindow, QToolBar, QAction, QWidget, QColorDialog, QOpenGLWidget, QInputDialog
from PyQt5.QtGui import QIcon, QPixmap, QColor, QPen, QPainter, QTabletEvent
from PyQt5.QtCore import QEvent, QObject, Qt, pyqtSignal, QPoint, pyqtSlot
from create_pen import create_pen_svg, create_using_pen_svg
from OpenGL.GL import glLineWidth, glEnable, GL_LINE_SMOOTH, GL_BLEND, GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA, glLoadIdentity, glOrtho, glBlendFunc
import qdarktheme 

class ScribbleObject:
    def __init__(self, path=[], color=QColor(255, 255, 255), thickness=3, thickness_path = []):
        self.path = path
        self.color = color
        self.thickness = thickness
        self.thickness_path = thickness_path

    def drawObject(self, painter):
        if len(self.path) < 2:
            return

        # painter.setPen(QPen(self.color, self.thickness, Qt.SolidLine))

        for i in range(len(self.path) - 1):
            p1 = self.path[i]
            p2 = self.path[i + 1]
            # print(self.thickness_path)
            width = self.thickness_path[i]

            # print(len(self.path), len(self.thickness_path))

            # print(len(self.path), len(self.thickness_path))
            painter.setPen(QPen(self.color, width, Qt.SolidLine))

            painter.drawLine(p1, p2)


class ScribbleWidget(QOpenGLWidget):

    __DEFAULT_PEN_WIDTH = 3

    _PenColor = QColor("#000000")
    _eraser_mode = False
    _pen_width = __DEFAULT_PEN_WIDTH
    _pressure_pen = False

    def __init__(self, parent=None):
        super(ScribbleWidget, self).__init__(parent)
        self.objects = []
        self.current_object = None
        self.erasing = False
        self.setMouseTracking(True)

    def eraser_mode(self, status: bool):
        self._eraser_mode = status

    def changeColor(self, color: str):
        self._PenColor = QColor(color)

    def delete_all(self, status: bool):
        self.objects = []
        self.update()

    def change_pen_thickness_func(self, num:int):
        self.__DEFAULT_PEN_WIDTH = num
        self._pen_width = self.__DEFAULT_PEN_WIDTH
        self.update()

    def initializeGL(self):
        glEnable(GL_LINE_SMOOTH)
        glEnable(GL_BLEND)
        glLineWidth(self._pen_width)
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)
        glLoadIdentity()
        glOrtho(0, self.width(), self.height(), 0, -1, 1)

    def paintGL(self):
        painter = QPainter(self)
        painter.beginNativePainting()

        for obj in self.objects:
            obj.drawObject(painter)

        if self.current_object:
            self.current_object.drawObject(painter)

        painter.endNativePainting() 

    def tabletEvent(self, event: QTabletEvent):
        pressure = event.pressure()
        # self._pen_width = int(pressure * 10)
        self._pen_width = int(pressure * 5) + self.__DEFAULT_PEN_WIDTH
        # print(self._pen_width)

        self._pressure_pen = True

        if not self._eraser_mode:
            if event.type() == event.TabletPress:
                self.current_object = ScribbleObject([event.pos()], self._PenColor, self._pen_width, [self._pen_width])
            elif event.type() == event.TabletMove and self.current_object:
                self.current_object.path.append(event.pos())
                self.current_object.thickness_path.append(self._pen_width)
                self.update()
            elif event.type() == event.TabletRelease and self.current_object:
                self.objects.append(self.current_object)
                self.current_object = None
                self._pressure_pen = False
                self._pen_width = self.__DEFAULT_PEN_WIDTH
                self.update()


    def mousePressEvent(self, event):

        if self._eraser_mode and event.button() == Qt.LeftButton:
            self.startErasing(event.pos())

        elif not self._pressure_pen:
            if event.button() == Qt.LeftButton:
                self.current_object = ScribbleObject([event.pos()], self._PenColor, self._pen_width, [self._pen_width])

    def mouseMoveEvent(self, event):
        if self._eraser_mode and event.buttons() == Qt.LeftButton:
            self.eraseLines(event.pos())
            self.update()
        elif not self._pressure_pen:
            if event.buttons() == Qt.LeftButton and self.current_object:
                self.current_object.path.append(event.pos())
                self.current_object.thickness_path.append(self._pen_width)
                self.update()

    def mouseReleaseEvent(self, event):
        if self._eraser_mode and event.button() == Qt.LeftButton:
            self.stopErasing()
        elif not self._pressure_pen:
            if event.button() == Qt.LeftButton and self.current_object:
                self.objects.append(self.current_object)
                self.current_object = None
                self.update()

    def eraseLines(self, pos):
        if not self.erasing:
            return

        objects_to_remove = []

        for obj in self.objects:
            nearest_point, nearest_distance = self.nearestPointOnLines(pos, obj.path)
            if nearest_distance < 10:  # Adjust this threshold as needed
                objects_to_remove.append(obj)

        for obj in objects_to_remove:
            self.objects.remove(obj)

    def startErasing(self, pos):
        self.erasing = True
        self.eraseLines(pos)

    def stopErasing(self):
        self.erasing = False

    def nearestPointOnLines(self, point, lines):
        nearest_point = None
        nearest_distance = float('inf')

        for i in range(len(lines) - 1):
            p1 = lines[i]
            p2 = lines[i + 1]
            nearest, distance = self.nearestPointOnLine(point, p1, p2)

            if distance < nearest_distance:
                nearest_distance = distance
                nearest_point = nearest

        return nearest_point, nearest_distance

    def nearestPointOnLine(self, point, line_start, line_end):
        x1, y1 = line_start.x(), line_start.y()
        x2, y2 = line_end.x(), line_end.y()
        x0, y0 = point.x(), point.y()

        dx, dy = x2 - x1, y2 - y1
        if dx == 0 and dy == 0:
            return line_start, ((x0 - x1) ** 2 + (y0 - y1) ** 2) ** 0.5

        t = ((x0 - x1) * dx + (y0 - y1) * dy) / (dx ** 2 + dy ** 2)
        t = max(0, min(1, t))

        nearest_x = x1 + t * dx
        nearest_y = y1 + t * dy

        nearest_point = QPoint(int(nearest_x), int(nearest_y))
        distance = ((x0 - nearest_x) ** 2 + (y0 - nearest_y) ** 2) ** 0.5

        return nearest_point, distance

class TransparentWindow(QMainWindow):
    def __init__(self):
        super().__init__()

        self.initUI()
        self.launch_toolbar()

        self.setWindowTitle("ScreenPrompt")
        self.setWindowIcon(QIcon('sources/logo.png'))

    def initUI(self):
        # Set the window to be transparent
        self.setWindowFlags(self.windowFlags() | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint)
        # self.setWindowFlags(Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.WindowTransparentForInput)
        # Set the window to Control Mode
        self.setAttribute(Qt.WA_TranslucentBackground, True)
        # self.setAttribute(Qt.WA_TransparentForMouseEvents, True)
        self.setAttribute(Qt.WA_PaintOnScreen, True)
        # self.setGeometry(100, 100, 400, 300)
        self.showFullScreen() 
        # self.setStyleSheet('background-color:transparent')

        # Set the Window to Drawing Mode

        # central_widget = QWidget(self)
        self.installEventFilter(self)

        self.ScribbleWidget = ScribbleWidget()
        # self.ScribbleWidget._close_signal.connect(self.reopen_canva)

        self.setCentralWidget(self.ScribbleWidget)

        # Data Saved 


    # def reopen_canva(self, path, color):
    #     self.ScribbleWidget = ScribbleWidget(path = path, color = color, regraph=True)
    #     self.ScribbleWidget._close_signal.connect(self.reopen_canva)

    #     self.child_window._switch_color.connect(self.ScribbleWidget.changeColor)
    #     self.child_window._eraser_mode.connect(self.ScribbleWidget.eraser_mode)
        # self.setCentralWidget(self.ScribbleWidget)

    def eventFilter(self, obj: QObject, event: QEvent) -> bool:
        if obj is self:
            # event.type() == QEvent.MouseButtonPress
            if event.type() == 5 or event.type() == 2:
                # print(event)

                return True

        return super(TransparentWindow, self).eventFilter(obj, event)


    def launch_toolbar(self):
        self.child_window = MyToolbarApp(self)
        self.child_window._switch_signal.connect(self.switch_status_of_window)
        self.child_window._switch_color.connect(self.ScribbleWidget.changeColor)
        self.child_window._eraser_mode.connect(self.ScribbleWidget.eraser_mode)
        self.child_window._change_thickness_signal.connect(self.ScribbleWidget.change_pen_thickness_func)
        self.child_window._delete_all_signal.connect(self.ScribbleWidget.delete_all)
        self.child_window.show()

    def switch_status_of_window(self, status:bool):
        current_flags = self.windowFlags()

        if status:
            self.setWindowFlags(current_flags | Qt.WindowTransparentForInput)
        else:
            self.setWindowFlags(current_flags & ~Qt.WindowTransparentForInput)
        # self.setAttribute(Qt.WA_TransparentForMouseEvents, status)

        self.show()

    def mousePressEvent(self, event):
        # Pass mouse events through to the underlying windows
        event.ignore()

    def closeEvent(self, event):
        # Perform any cleanup or additional actions before closing
        super().closeEvent(event)
        self.child_window.close()

class MyToolbarApp(QMainWindow):

    _switch_signal = pyqtSignal(bool)
    _switch_color = pyqtSignal(str)
    _eraser_mode = pyqtSignal(bool)
    _delete_all_signal = pyqtSignal(bool)
    _change_thickness_signal = pyqtSignal(int)

    __switch_status = True  # On by Default by which False status will be sent to turn it off

    __LIST_OF_PEN:dict = {}

    __PEN_ACTIONS = []

    _last_index = 0

    __user_profile_path = "" 

    __current_selected_pen_action:QAction = None
    __current_selected_pen_action_index:int = 0

    def __init__(self, parent=None):
        super().__init__(parent)
        self.init_user_profile()
        self.initUI() 

    def init_user_profile(self):
        user_profile_name = "user.json" 
        self.__user_profile_path = os.path.realpath(user_profile_name) 

        if os.path.exists(self.__user_profile_path):
            with open(self.__user_profile_path, 'r') as json_file:
                data = json.load(json_file)

            self.__LIST_OF_PEN = data
            # return True
        else:
            self.__LIST_OF_PEN = {
                "Pen 1" : "#000000",
                "Pen 2" : "#d81324",
                "Pen 3" : "#015fab",
            }

            self.save_pen_data(self.__user_profile_path)
        # return False
            
    def save_pen_data(self, path):
        with open(path, 'w') as json_file:
            json.dump(self.__LIST_OF_PEN, json_file)

    def rearrange_dict_saved(self): 
        new_dict = {} 
        counter = 1 
        for key, value in self.__LIST_OF_PEN.items():
            new_key = "Pen " + str(counter)
            new_dict[new_key] = value
            counter += 1 

        self.__LIST_OF_PEN = new_dict 

    def initUI(self):
        self.setWindowTitle("Painto")
        self.setWindowFlag(Qt.WindowStaysOnTopHint)
        self.setGeometry(50, 50, 800, 50)
        self.setFixedSize(800, 60)
        self.setWindowIcon(QIcon('sources/logo.png'))

        central_widget = QWidget(self)
        self.setCentralWidget(central_widget)

        # layout = QVBoxLayout(central_widget)

        # self.status_label = QLabel("No action triggered", self)
        # layout.addWidget(self.status_label)

        # Create a vertical toolbar
        self.toolbar = QToolBar("ToolBar", self)
        self.toolbar.setOrientation(Qt.Vertical)
        self.toolbar.setStyleSheet("QToolBar { border: 0px; }") 
        self.toolbar.setMovable(False)
        self.addToolBar(self.toolbar)

        # Set a custom icon size (e.g., 64x64 pixels)
        icon_size = 50
        self.toolbar.setIconSize(QPixmap(icon_size, icon_size).size())


        # self.__LIST_OF_PEN = {
        #     "Pen 1" : "#000000",
        #     "Pen 2" : "#d81324",
        #     "Pen 3" : "#015fab",
        # }

        # Create Switch Button 
        self.switch_btn_action = QAction(QIcon(f"sources/switch_trigger.png"), "Switch to Desktop / Window", self)
        self.switch_btn_action.triggered.connect(self.switch_to_desktop_or_window)
        self.toolbar.addAction(self.switch_btn_action)
        
        self.eraser = QAction(QIcon("sources/eraser.png"), "Eraser", self)
        self.eraser.triggered.connect(self.use_eraser)
        self.toolbar.addAction(self.eraser)

        self.delete_all = QAction(QIcon("sources/delete_all.png"), "Delete All Inks", self)
        self.delete_all.triggered.connect(self.delete_allInks)
        # self.delete_all.
        self.toolbar.addAction(self.delete_all)

        self.change_thickness_action = QAction(QIcon(f"sources/thickness.png"), "Change Pen's thickness", self)
        self.change_thickness_action.triggered.connect(self.change_thickness)
        self.toolbar.addAction(self.change_thickness_action)

        self.create_toolbar_actions()

        
        # Create a QAction for adding widgets to the toolbar
        add_widget_action = QAction(QIcon("sources/add.png"), "Add Pens", self)
        add_widget_action.triggered.connect(self.add_widgets_to_toolbar)
        self.toolbar.addAction(add_widget_action) 

        self.addToolBarBreak()

        # Pen Toolbar 
        self.pen_toolbar = QToolBar("Pen's Toolbar") 
        self.pen_toolbar.setIconSize(QPixmap(icon_size, icon_size).size()) 
        self.pen_toolbar.setMovable(False)
        self.addToolBar(self.pen_toolbar) 

        self.delete_current_pen = QAction(QIcon("sources/delete_pen.png"), "Delete Pens", self)
        self.delete_current_pen.triggered.connect(self.delete_current_pen_func)
        self.pen_toolbar.addAction(self.delete_current_pen) 

        self.pen_toolbar.hide() 

    def delete_current_pen_func(self): 
        # self.__current_selected_pen_action.deleteLater() 
        self.toolbar.removeAction(self.__current_selected_pen_action) 
        name_to_delete = "Pen " + str(self.__current_selected_pen_action_index) 
        self.__LIST_OF_PEN.pop(name_to_delete)

        self.pen_toolbar.hide() 
        self.setFixedSize(800, 60) 

    def change_thickness(self):
        force_switch = False
        if self.__switch_status:
            self.switch_to_desktop_or_window()
            force_switch = True
        
        items = ['1px', '3px', '5px', '7px', '9px']
        selected_items, ok = QInputDialog.getItem(self, 'Select Thicknesses', 'Choose at least three items:', items, 1, False)
        if ok:
            if selected_items == "1px":
                self._change_thickness_signal.emit(1)

            elif selected_items == "3px":
                self._change_thickness_signal.emit(3)

            elif selected_items == "5px":
                self._change_thickness_signal.emit(5)

            elif selected_items == "7px":
                self._change_thickness_signal.emit(7)

            else:
                self._change_thickness_signal.emit(9)

        if force_switch:
            self.switch_to_desktop_or_window()

    def delete_allInks(self):
        self._delete_all_signal.emit(True)

    def use_eraser(self):
        self._eraser_mode.emit(True)
        self.eraser.setIcon(QIcon("sources/eraser_using.png"))
        if not self.__switch_status:
            self.switch_to_desktop_or_window()

    def switch_to_desktop_or_window(self):
        # self._eraser_mode.emit(False)
        self._switch_signal.emit(self.__switch_status) 
        if self.__switch_status:
            self.__switch_status = False
            self.switch_btn_action.setIcon(QIcon("sources/swtich.png"))
        else:
            self.__switch_status = True
            self.switch_btn_action.setIcon(QIcon("sources/switch_trigger.png"))

    def create_toolbar_actions(self):
        index = 1
        for action_text, action_value in self.__LIST_OF_PEN.items():
            action = QAction(QIcon(f"sources/pen_{index}.svg"), action_text, self)
            action.triggered.connect(lambda _, value=action_value, index = index, action = action: self.pen_trigger(value, index, action))
            self.toolbar.addAction(action)
            self.__PEN_ACTIONS.append(action)
            create_pen_svg(self.__LIST_OF_PEN[action_text], index)
            create_using_pen_svg(self.__LIST_OF_PEN[action_text], index)
            index += 1

        self._last_index = index

    def pen_trigger(self, color, index, action):
        # Change window size for function of pens. 
        self.__current_selected_pen_action = action
        self.__current_selected_pen_action_index = index
        self.setFixedSize(800, 120)
        self.pen_toolbar.show() 

        self._eraser_mode.emit(False)
        self.eraser.setIcon(QIcon("sources/eraser.png"))
        self._switch_color.emit(color)
        self.restore_all_other_pens()
        action.setIcon(QIcon(f"sources/pen_using_{index}.svg"))

        if not self.__switch_status:
            self.switch_to_desktop_or_window()

    def restore_all_other_pens(self):
        index = 1
        for each_action in self.__PEN_ACTIONS:
            each_action.setIcon(QIcon(f"sources/pen_{index}.svg"))
            index += 1

    def show_color_dialog(self):
        color = QColorDialog.getColor()

        if color.isValid():
            color_name = color.name()
            self.centralWidget().setStyleSheet(f"background-color: {color_name};")

    def pick_new_color(self) -> str:
        """
        return: hex
        """
        force_switch = False

        if self.__switch_status:
            self.switch_to_desktop_or_window()
            force_switch = True
        dialog = QColorDialog(Qt.white)
        dialog.setWindowIcon(QIcon('sources/logo.png'))
        dialog.show()
        dialog.exec()
        dialog.close()
        color = dialog.selectedColor()
        # color = dialog.getColor(title="Choose Your Pen's Color")

        if force_switch:
            self.switch_to_desktop_or_window()

        if color.isValid():
            return color.name()
        
        # Default Color
        return None

    def add_widgets_to_toolbar(self):
        # Create and add widgets to the toolbar
        index = self._last_index
        self._last_index += 1
        
        color = self.pick_new_color()

        if color is not None:
            create_pen_svg(color, index)
            create_using_pen_svg(color, index)
            
            new_widget = QAction(QIcon(f"sources/pen_{index}.svg"), f"Pen {index}", self)
            new_widget.triggered.connect(lambda _, value=color, index = index, action = new_widget: self.pen_trigger(value, index, action))
            self.toolbar.insertAction(self.toolbar.actions()[-1], new_widget)
            self.__PEN_ACTIONS.append(new_widget)

            self.__LIST_OF_PEN[f"Pen {index}"] = color

    def closeEvent(self, event):
        self.parent().close()
        super().closeEvent(event)

        self.rearrange_dict_saved() 
        self.save_pen_data(self.__user_profile_path) 
        
if __name__ == '__main__':
    qdarktheme.enable_hi_dpi()
    app = QApplication(sys.argv)

    # PyQt5 Theme 
    # Apply the complete dark theme to your Qt App.
    qdarktheme.setup_theme("auto") 

    window = TransparentWindow()
    window.show()
    
    sys.exit(app.exec_())
